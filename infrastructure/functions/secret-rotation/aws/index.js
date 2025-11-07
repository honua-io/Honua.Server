/**
 * AWS Lambda Function - Automated Secret Rotation for HonuaIO
 *
 * Rotates:
 * - PostgreSQL database passwords
 * - API keys stored in AWS Secrets Manager
 * - JWT signing keys
 *
 * Triggered by:
 * - EventBridge schedule (every 90 days)
 * - Manual invocation
 * - Secrets Manager rotation schedule
 */

// AWS SDK v3 - Modular imports
const {
  SecretsManagerClient,
  GetSecretValueCommand,
  PutSecretValueCommand,
  DescribeSecretCommand,
  UpdateSecretVersionStageCommand,
  ListSecretsCommand,
  RotateSecretCommand
} = require('@aws-sdk/client-secrets-manager');
const { SNSClient, PublishCommand } = require('@aws-sdk/client-sns');
const { SSMClient, PutParameterCommand } = require('@aws-sdk/client-ssm');
const { Client } = require('pg');

// AWS SDK v3 Clients
const secretsManagerClient = new SecretsManagerClient({});
const snsClient = new SNSClient({});
const ssmClient = new SSMClient({});

/**
 * Lambda handler - routes to appropriate rotation function
 */
exports.handler = async (event) => {
  console.log('Secret rotation triggered:', JSON.stringify(event, null, 2));

  try {
    const { Step, Token, SecretId } = event;

    // Handle Secrets Manager rotation steps
    if (Step && Token && SecretId) {
      return await handleSecretsManagerRotation(event);
    }

    // Handle EventBridge scheduled rotation
    if (event.source === 'aws.events') {
      return await handleScheduledRotation(event);
    }

    // Handle manual invocation
    if (event.secretType) {
      return await rotateSecret(event.secretType, event.secretId);
    }

    throw new Error('Unknown event type');

  } catch (error) {
    console.error('Rotation failed:', error);
    await sendNotification('ERROR', `Secret rotation failed: ${error.message}`);
    throw error;
  }
};

/**
 * Handle AWS Secrets Manager automatic rotation
 */
async function handleSecretsManagerRotation(event) {
  const { Step, Token, SecretId } = event;

  console.log(`Processing rotation step: ${Step} for secret: ${SecretId}`);

  switch (Step) {
    case 'createSecret':
      await createSecret(SecretId, Token);
      break;

    case 'setSecret':
      await setSecret(SecretId, Token);
      break;

    case 'testSecret':
      await testSecret(SecretId, Token);
      break;

    case 'finishSecret':
      await finishSecret(SecretId, Token);
      break;

    default:
      throw new Error(`Unknown rotation step: ${Step}`);
  }

  return { statusCode: 200, body: 'Rotation step completed' };
}

/**
 * Handle EventBridge scheduled rotation
 */
async function handleScheduledRotation(event) {
  console.log('Processing scheduled rotation');

  const rotations = [];

  // Get all secrets tagged for rotation
  const secrets = await listSecretsForRotation();

  for (const secret of secrets) {
    try {
      console.log(`Rotating secret: ${secret.Name}`);
      await rotateSecretById(secret.Name);
      rotations.push({ secret: secret.Name, status: 'success' });
    } catch (error) {
      console.error(`Failed to rotate ${secret.Name}:`, error);
      rotations.push({ secret: secret.Name, status: 'failed', error: error.message });
    }
  }

  // Send summary notification
  await sendRotationSummary(rotations);

  return { statusCode: 200, rotations };
}

/**
 * Step 1: Create new secret version
 */
async function createSecret(secretId, token) {
  console.log(`Creating new secret version for: ${secretId}`);

  // Get current secret
  const currentSecret = await secretsManagerClient.send(
    new GetSecretValueCommand({
      SecretId: secretId,
      VersionStage: 'AWSCURRENT'
    })
  );

  const currentValue = JSON.parse(currentSecret.SecretString);
  const secretType = currentValue.type || 'postgresql';

  let newSecretValue;

  switch (secretType) {
    case 'postgresql':
      newSecretValue = await generatePostgresPassword(currentValue);
      break;

    case 'api-key':
      newSecretValue = await generateApiKey(currentValue);
      break;

    case 'jwt-signing-key':
      newSecretValue = await generateJwtKey(currentValue);
      break;

    default:
      throw new Error(`Unknown secret type: ${secretType}`);
  }

  // Store new secret with AWSPENDING stage
  await secretsManagerClient.send(
    new PutSecretValueCommand({
      SecretId: secretId,
      ClientRequestToken: token,
      SecretString: JSON.stringify(newSecretValue),
      VersionStages: ['AWSPENDING']
    })
  );

  console.log('New secret version created');
}

/**
 * Step 2: Set the secret in the target service
 */
async function setSecret(secretId, token) {
  console.log(`Setting new secret in target service: ${secretId}`);

  // Get pending secret
  const pendingSecret = await secretsManagerClient.send(
    new GetSecretValueCommand({
      SecretId: secretId,
      VersionId: token,
      VersionStage: 'AWSPENDING'
    })
  );

  const secretValue = JSON.parse(pendingSecret.SecretString);
  const secretType = secretValue.type;

  switch (secretType) {
    case 'postgresql':
      await setPostgresPassword(secretValue);
      break;

    case 'api-key':
      await setApiKey(secretValue);
      break;

    case 'jwt-signing-key':
      await setJwtKey(secretValue);
      break;

    default:
      throw new Error(`Unknown secret type: ${secretType}`);
  }

  console.log('Secret set in target service');
}

/**
 * Step 3: Test the new secret
 */
async function testSecret(secretId, token) {
  console.log(`Testing new secret: ${secretId}`);

  // Get pending secret
  const pendingSecret = await secretsManagerClient.send(
    new GetSecretValueCommand({
      SecretId: secretId,
      VersionId: token,
      VersionStage: 'AWSPENDING'
    })
  );

  const secretValue = JSON.parse(pendingSecret.SecretString);
  const secretType = secretValue.type;

  switch (secretType) {
    case 'postgresql':
      await testPostgresConnection(secretValue);
      break;

    case 'api-key':
      await testApiKey(secretValue);
      break;

    case 'jwt-signing-key':
      await testJwtKey(secretValue);
      break;

    default:
      throw new Error(`Unknown secret type: ${secretType}`);
  }

  console.log('Secret test successful');
}

/**
 * Step 4: Finalize the rotation
 */
async function finishSecret(secretId, token) {
  console.log(`Finalizing rotation for: ${secretId}`);

  // Get current version
  const metadata = await secretsManagerClient.send(
    new DescribeSecretCommand({
      SecretId: secretId
    })
  );

  let currentVersion;
  for (const [versionId, stages] of Object.entries(metadata.VersionIdsToStages)) {
    if (stages.includes('AWSCURRENT')) {
      currentVersion = versionId;
      break;
    }
  }

  // Move AWSCURRENT to AWSPREVIOUS
  await secretsManagerClient.send(
    new UpdateSecretVersionStageCommand({
      SecretId: secretId,
      VersionStage: 'AWSCURRENT',
      MoveToVersionId: token,
      RemoveFromVersionId: currentVersion
    })
  );

  console.log('Rotation completed successfully');

  // Send success notification
  await sendNotification('SUCCESS', `Secret rotated successfully: ${secretId}`);
}

/**
 * Generate new PostgreSQL password
 */
async function generatePostgresPassword(currentValue) {
  const crypto = require('crypto');

  // Generate strong password: 32 characters, alphanumeric + special chars
  const newPassword = crypto.randomBytes(24).toString('base64').slice(0, 32);

  return {
    ...currentValue,
    password: newPassword,
    lastRotated: new Date().toISOString()
  };
}

/**
 * Set new PostgreSQL password
 */
async function setPostgresPassword(secretValue) {
  const { host, port, database, username, password } = secretValue;

  // Connect with current master password
  const masterSecretId = process.env.POSTGRES_MASTER_SECRET_ID;
  const masterSecret = await secretsManagerClient.send(
    new GetSecretValueCommand({
      SecretId: masterSecretId
    })
  );
  const masterValue = JSON.parse(masterSecret.SecretString);

  // SECURITY: SSL certificate verification is enabled to prevent man-in-the-middle attacks
  // and ensure we're connecting to the legitimate database server. This protects sensitive
  // credentials during transmission.
  const client = new Client({
    host,
    port: port || 5432,
    database: database || 'postgres',
    user: masterValue.username,
    password: masterValue.password,
    ssl: { rejectUnauthorized: true }
  });

  try {
    await client.connect();

    // Change password for the application user
    const query = `ALTER USER ${username} WITH PASSWORD $1`;
    await client.query(query, [password]);

    console.log(`Password updated for user: ${username}`);
  } catch (error) {
    // Provide helpful error message for SSL certificate issues
    if (error.code === 'UNABLE_TO_VERIFY_LEAF_SIGNATURE' ||
        error.code === 'SELF_SIGNED_CERT_IN_CHAIN' ||
        error.code === 'CERT_HAS_EXPIRED') {
      throw new Error(
        `SSL certificate verification failed: ${error.message}. ` +
        `Ensure your database has a valid SSL certificate. ` +
        `For RDS, verify the RDS CA certificate is properly configured.`
      );
    }
    throw error;
  } finally {
    await client.end();
  }
}

/**
 * Test PostgreSQL connection with new password
 */
async function testPostgresConnection(secretValue) {
  const { host, port, database, username, password } = secretValue;

  // SECURITY: SSL certificate verification is enabled to prevent man-in-the-middle attacks
  // and ensure we're connecting to the legitimate database server. This protects sensitive
  // credentials during transmission.
  const client = new Client({
    host,
    port: port || 5432,
    database: database || 'honua',
    user: username,
    password,
    ssl: { rejectUnauthorized: true },
    connectionTimeoutMillis: 5000
  });

  try {
    await client.connect();
    const result = await client.query('SELECT 1 as test');

    if (result.rows[0].test !== 1) {
      throw new Error('Connection test query failed');
    }

    console.log('PostgreSQL connection test successful');
  } catch (error) {
    // Provide helpful error message for SSL certificate issues
    if (error.code === 'UNABLE_TO_VERIFY_LEAF_SIGNATURE' ||
        error.code === 'SELF_SIGNED_CERT_IN_CHAIN' ||
        error.code === 'CERT_HAS_EXPIRED') {
      throw new Error(
        `SSL certificate verification failed during connection test: ${error.message}. ` +
        `Ensure your database has a valid SSL certificate. ` +
        `For RDS, verify the RDS CA certificate is properly configured.`
      );
    }
    throw error;
  } finally {
    await client.end();
  }
}

/**
 * Generate new API key
 */
async function generateApiKey(currentValue) {
  const crypto = require('crypto');

  // Generate API key: prefix + random bytes
  const prefix = currentValue.prefix || 'honua';
  const randomPart = crypto.randomBytes(32).toString('hex');
  const newApiKey = `${prefix}_${randomPart}`;

  return {
    ...currentValue,
    apiKey: newApiKey,
    lastRotated: new Date().toISOString()
  };
}

/**
 * Set new API key (update in database)
 */
async function setApiKey(secretValue) {
  // Get database connection
  const dbSecretId = process.env.DATABASE_SECRET_ID;
  const dbSecret = await secretsManagerClient.send(
    new GetSecretValueCommand({
      SecretId: dbSecretId
    })
  );
  const dbValue = JSON.parse(dbSecret.SecretString);

  // SECURITY: SSL certificate verification is enabled to prevent man-in-the-middle attacks
  // and ensure we're connecting to the legitimate database server. This protects sensitive
  // credentials during transmission.
  const client = new Client({
    host: dbValue.host,
    port: dbValue.port || 5432,
    database: dbValue.database,
    user: dbValue.username,
    password: dbValue.password,
    ssl: { rejectUnauthorized: true }
  });

  try {
    await client.connect();

    // Update API key in database
    const query = `
      UPDATE api_keys
      SET key_hash = crypt($1, gen_salt('bf')),
          updated_at = NOW(),
          rotated_at = NOW()
      WHERE key_id = $2
    `;
    await client.query(query, [secretValue.apiKey, secretValue.keyId]);

    console.log(`API key updated for keyId: ${secretValue.keyId}`);
  } catch (error) {
    // Provide helpful error message for SSL certificate issues
    if (error.code === 'UNABLE_TO_VERIFY_LEAF_SIGNATURE' ||
        error.code === 'SELF_SIGNED_CERT_IN_CHAIN' ||
        error.code === 'CERT_HAS_EXPIRED') {
      throw new Error(
        `SSL certificate verification failed while updating API key: ${error.message}. ` +
        `Ensure your database has a valid SSL certificate. ` +
        `For RDS, verify the RDS CA certificate is properly configured.`
      );
    }
    throw error;
  } finally {
    await client.end();
  }
}

/**
 * Test API key
 */
async function testApiKey(secretValue) {
  // Test by making a request to the API
  const apiEndpoint = process.env.API_ENDPOINT;
  const https = require('https');

  return new Promise((resolve, reject) => {
    const options = {
      hostname: apiEndpoint,
      path: '/healthz',
      method: 'GET',
      headers: {
        'X-API-Key': secretValue.apiKey
      }
    };

    const req = https.request(options, (res) => {
      if (res.statusCode === 200) {
        console.log('API key test successful');
        resolve();
      } else {
        reject(new Error(`API key test failed with status: ${res.statusCode}`));
      }
    });

    req.on('error', reject);
    req.setTimeout(5000, () => reject(new Error('API key test timeout')));
    req.end();
  });
}

/**
 * Generate new JWT signing key
 */
async function generateJwtKey(currentValue) {
  const crypto = require('crypto');

  // Generate 256-bit signing key
  const newKey = crypto.randomBytes(32).toString('base64');

  return {
    ...currentValue,
    signingKey: newKey,
    lastRotated: new Date().toISOString()
  };
}

/**
 * Set new JWT signing key (update in Parameter Store)
 */
async function setJwtKey(secretValue) {
  await ssmClient.send(
    new PutParameterCommand({
      Name: secretValue.parameterName || '/honua/jwt/signing-key',
      Value: secretValue.signingKey,
      Type: 'SecureString',
      Overwrite: true
    })
  );

  console.log('JWT signing key updated in Parameter Store');
}

/**
 * Test JWT signing key
 */
async function testJwtKey(secretValue) {
  // Simple validation - ensure key is base64 and correct length
  const keyBuffer = Buffer.from(secretValue.signingKey, 'base64');

  if (keyBuffer.length !== 32) {
    throw new Error('JWT key must be 32 bytes (256 bits)');
  }

  console.log('JWT key validation successful');
}

/**
 * List secrets tagged for automatic rotation
 */
async function listSecretsForRotation() {
  const secrets = await secretsManagerClient.send(
    new ListSecretsCommand({
      Filters: [
        {
          Key: 'tag-key',
          Values: ['AutoRotate']
        },
        {
          Key: 'tag-value',
          Values: ['true']
        }
      ]
    })
  );

  return secrets.SecretList;
}

/**
 * Rotate a secret by ID
 */
async function rotateSecretById(secretId) {
  await secretsManagerClient.send(
    new RotateSecretCommand({
      SecretId: secretId,
      RotationLambdaARN: process.env.AWS_LAMBDA_FUNCTION_NAME
    })
  );
}

/**
 * Send notification via SNS
 */
async function sendNotification(status, message) {
  const topicArn = process.env.SNS_TOPIC_ARN;

  if (!topicArn) {
    console.warn('SNS_TOPIC_ARN not set, skipping notification');
    return;
  }

  await snsClient.send(
    new PublishCommand({
      TopicArn: topicArn,
      Subject: `[HonuaIO] Secret Rotation ${status}`,
      Message: `${message}\n\nTimestamp: ${new Date().toISOString()}`,
      MessageAttributes: {
        status: {
          DataType: 'String',
          StringValue: status
        }
      }
    })
  );

  console.log('Notification sent');
}

/**
 * Send rotation summary notification
 */
async function sendRotationSummary(rotations) {
  const total = rotations.length;
  const successful = rotations.filter(r => r.status === 'success').length;
  const failed = rotations.filter(r => r.status === 'failed').length;

  const message = `
Secret Rotation Summary
========================

Total Secrets: ${total}
Successful: ${successful}
Failed: ${failed}

Details:
${rotations.map(r => `- ${r.secret}: ${r.status}${r.error ? ` (${r.error})` : ''}`).join('\n')}

Timestamp: ${new Date().toISOString()}
`;

  await sendNotification(failed > 0 ? 'WARNING' : 'SUCCESS', message);
}
