/**
 * Azure Function - Automated Secret Rotation for HonuaIO
 *
 * Rotates:
 * - PostgreSQL database passwords
 * - API keys stored in Azure Key Vault
 * - JWT signing keys
 *
 * Triggered by:
 * - Timer (every 90 days)
 * - HTTP request (manual)
 * - Key Vault events
 */

const { SecretClient } = require('@azure/keyvault-secrets');
const { DefaultAzureCredential } = require('@azure/identity');
const { Client } = require('pg');

// Azure clients
const credential = new DefaultAzureCredential();
let secretClient;
let notificationClient;

/**
 * Azure Function entry point
 */
module.exports = async function (context, timerTrigger, httpTrigger) {
  context.log('Secret rotation function started');

  try {
    // Initialize clients
    const keyVaultUrl = process.env.KEY_VAULT_URL;
    if (!keyVaultUrl) {
      throw new Error('KEY_VAULT_URL environment variable not set');
    }

    secretClient = new SecretClient(keyVaultUrl, credential);

    // Determine trigger type
    let rotationType = 'scheduled';
    let secretsToRotate = [];

    if (httpTrigger) {
      // Manual HTTP trigger
      rotationType = 'manual';
      const requestBody = httpTrigger.body || {};

      if (requestBody.secretName) {
        secretsToRotate = [requestBody.secretName];
      } else {
        secretsToRotate = await listSecretsForRotation();
      }
    } else if (timerTrigger) {
      // Timer trigger (scheduled)
      secretsToRotate = await listSecretsForRotation();
    }

    context.log(`Rotation type: ${rotationType}`);
    context.log(`Secrets to rotate: ${secretsToRotate.length}`);

    // Rotate each secret
    const results = [];
    for (const secretName of secretsToRotate) {
      try {
        context.log(`Rotating secret: ${secretName}`);
        await rotateSecret(context, secretName);
        results.push({ secretName, status: 'success' });
      } catch (error) {
        context.log.error(`Failed to rotate ${secretName}:`, error);
        results.push({ secretName, status: 'failed', error: error.message });
      }
    }

    // Send notification
    await sendRotationSummary(context, results);

    // Return response for HTTP trigger
    if (httpTrigger) {
      context.res = {
        status: 200,
        body: {
          message: 'Secret rotation completed',
          results
        }
      };
    }

    context.log('Secret rotation completed successfully');

  } catch (error) {
    context.log.error('Secret rotation failed:', error);

    await sendNotification(context, 'ERROR', `Secret rotation failed: ${error.message}`);

    if (httpTrigger) {
      context.res = {
        status: 500,
        body: {
          error: error.message
        }
      };
    }

    throw error;
  }
};

/**
 * List secrets tagged for automatic rotation
 */
async function listSecretsForRotation() {
  const secrets = [];

  for await (const secretProperties of secretClient.listPropertiesOfSecrets()) {
    const tags = secretProperties.tags || {};

    if (tags.AutoRotate === 'true' || tags.autoRotate === 'true') {
      secrets.push(secretProperties.name);
    }
  }

  return secrets;
}

/**
 * Rotate a single secret
 */
async function rotateSecret(context, secretName) {
  context.log(`Starting rotation for: ${secretName}`);

  // Get current secret
  const currentSecret = await secretClient.getSecret(secretName);
  const secretType = currentSecret.properties.tags?.type || 'postgresql';

  let newSecretValue;

  switch (secretType) {
    case 'postgresql':
      newSecretValue = await rotatePostgresPassword(context, currentSecret);
      break;

    case 'api-key':
      newSecretValue = await rotateApiKey(context, currentSecret);
      break;

    case 'jwt-signing-key':
      newSecretValue = await rotateJwtKey(context, currentSecret);
      break;

    default:
      throw new Error(`Unknown secret type: ${secretType}`);
  }

  // Test new secret before committing
  await testSecret(context, secretType, newSecretValue);

  // Store new secret in Key Vault
  await secretClient.setSecret(secretName, newSecretValue, {
    tags: {
      ...currentSecret.properties.tags,
      lastRotated: new Date().toISOString()
    }
  });

  context.log(`Successfully rotated: ${secretName}`);
}

/**
 * Rotate PostgreSQL password
 */
async function rotatePostgresPassword(context, currentSecret) {
  const crypto = require('crypto');
  const currentValue = JSON.parse(currentSecret.value);

  // Generate new password
  const newPassword = crypto.randomBytes(24).toString('base64').slice(0, 32);

  context.log(`Updating PostgreSQL password for user: ${currentValue.username}`);

  // Connect to PostgreSQL with master credentials
  const masterSecretName = process.env.POSTGRES_MASTER_SECRET_NAME;
  const masterSecret = await secretClient.getSecret(masterSecretName);
  const masterValue = JSON.parse(masterSecret.value);

  const client = new Client({
    host: currentValue.host,
    port: currentValue.port || 5432,
    database: currentValue.database || 'postgres',
    user: masterValue.username,
    password: masterValue.password,
    ssl: { rejectUnauthorized: false }
  });

  try {
    await client.connect();

    // Change password
    const query = `ALTER USER ${currentValue.username} WITH PASSWORD $1`;
    await client.query(query, [newPassword]);

    context.log('Password updated in PostgreSQL');
  } finally {
    await client.end();
  }

  // Return new secret value
  return JSON.stringify({
    ...currentValue,
    password: newPassword,
    lastRotated: new Date().toISOString()
  });
}

/**
 * Rotate API key
 */
async function rotateApiKey(context, currentSecret) {
  const crypto = require('crypto');
  const currentValue = JSON.parse(currentSecret.value);

  // Generate new API key
  const prefix = currentValue.prefix || 'honua';
  const randomPart = crypto.randomBytes(32).toString('hex');
  const newApiKey = `${prefix}_${randomPart}`;

  context.log(`Updating API key for keyId: ${currentValue.keyId}`);

  // Get database connection
  const dbSecretName = process.env.DATABASE_SECRET_NAME;
  const dbSecret = await secretClient.getSecret(dbSecretName);
  const dbValue = JSON.parse(dbSecret.value);

  const client = new Client({
    host: dbValue.host,
    port: dbValue.port || 5432,
    database: dbValue.database,
    user: dbValue.username,
    password: dbValue.password,
    ssl: { rejectUnauthorized: false }
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
    await client.query(query, [newApiKey, currentValue.keyId]);

    context.log('API key updated in database');
  } finally {
    await client.end();
  }

  // Return new secret value
  return JSON.stringify({
    ...currentValue,
    apiKey: newApiKey,
    lastRotated: new Date().toISOString()
  });
}

/**
 * Rotate JWT signing key
 */
async function rotateJwtKey(context, currentSecret) {
  const crypto = require('crypto');
  const currentValue = JSON.parse(currentSecret.value);

  // Generate new 256-bit signing key
  const newKey = crypto.randomBytes(32).toString('base64');

  context.log('Generated new JWT signing key');

  // Return new secret value
  return JSON.stringify({
    ...currentValue,
    signingKey: newKey,
    lastRotated: new Date().toISOString()
  });
}

/**
 * Test a rotated secret
 */
async function testSecret(context, secretType, secretValue) {
  context.log(`Testing ${secretType} secret`);

  const value = JSON.parse(secretValue);

  switch (secretType) {
    case 'postgresql':
      await testPostgresConnection(value);
      break;

    case 'api-key':
      await testApiKey(value);
      break;

    case 'jwt-signing-key':
      await testJwtKey(value);
      break;

    default:
      throw new Error(`Unknown secret type: ${secretType}`);
  }

  context.log('Secret test successful');
}

/**
 * Test PostgreSQL connection
 */
async function testPostgresConnection(secretValue) {
  const client = new Client({
    host: secretValue.host,
    port: secretValue.port || 5432,
    database: secretValue.database || 'honua',
    user: secretValue.username,
    password: secretValue.password,
    ssl: { rejectUnauthorized: false },
    connectionTimeoutMillis: 5000
  });

  try {
    await client.connect();
    const result = await client.query('SELECT 1 as test');

    if (result.rows[0].test !== 1) {
      throw new Error('Connection test query failed');
    }
  } finally {
    await client.end();
  }
}

/**
 * Test API key
 */
async function testApiKey(secretValue) {
  const https = require('https');
  const apiEndpoint = process.env.API_ENDPOINT;

  if (!apiEndpoint) {
    console.warn('API_ENDPOINT not set, skipping API key test');
    return;
  }

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
 * Test JWT signing key
 */
async function testJwtKey(secretValue) {
  const keyBuffer = Buffer.from(secretValue.signingKey, 'base64');

  if (keyBuffer.length !== 32) {
    throw new Error('JWT key must be 32 bytes (256 bits)');
  }
}

/**
 * Send notification
 */
async function sendNotification(context, status, message) {
  const notificationUrl = process.env.NOTIFICATION_WEBHOOK_URL;

  if (!notificationUrl) {
    context.log.warn('NOTIFICATION_WEBHOOK_URL not set, skipping notification');
    return;
  }

  const https = require('https');
  const url = new URL(notificationUrl);

  const payload = JSON.stringify({
    title: `[HonuaIO] Secret Rotation ${status}`,
    text: message,
    timestamp: new Date().toISOString()
  });

  return new Promise((resolve, reject) => {
    const options = {
      hostname: url.hostname,
      port: url.port || 443,
      path: url.pathname + url.search,
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(payload)
      }
    };

    const req = https.request(options, (res) => {
      if (res.statusCode >= 200 && res.statusCode < 300) {
        context.log('Notification sent successfully');
        resolve();
      } else {
        context.log.warn(`Notification failed with status: ${res.statusCode}`);
        resolve(); // Don't fail rotation if notification fails
      }
    });

    req.on('error', (error) => {
      context.log.error('Notification error:', error);
      resolve(); // Don't fail rotation if notification fails
    });

    req.write(payload);
    req.end();
  });
}

/**
 * Send rotation summary
 */
async function sendRotationSummary(context, results) {
  const total = results.length;
  const successful = results.filter(r => r.status === 'success').length;
  const failed = results.filter(r => r.status === 'failed').length;

  const message = `
Secret Rotation Summary
========================

Total Secrets: ${total}
Successful: ${successful}
Failed: ${failed}

Details:
${results.map(r => `- ${r.secretName}: ${r.status}${r.error ? ` (${r.error})` : ''}`).join('\n')}

Timestamp: ${new Date().toISOString()}
`;

  await sendNotification(context, failed > 0 ? 'WARNING' : 'SUCCESS', message);
}
