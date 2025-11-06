/* eslint-disable no-console */
import { SQL } from '@carto/toolkit';

async function main() {
  if (typeof window === 'undefined') {
    global.window = global;
  }

  if (typeof fetch === 'undefined') {
    const { default: fetchImpl } = await import('node-fetch');
    global.fetch = fetchImpl;
  }

  const [, , baseUrl, datasetId] = process.argv;

  if (!baseUrl || !datasetId) {
    console.error('Usage: node carto-toolkit.smoke.mjs <baseUrl> <datasetId>');
    process.exit(1);
  }

  const normalizedBase = baseUrl.replace(/\/$/, '');
  const server = `${normalizedBase}/carto/`;

  const sql = new SQL('honua', 'default_public', server);

  try {
    const rowsResult = await sql.query(`SELECT * FROM ${datasetId} LIMIT 2`);
    const countResult = await sql.query(`SELECT count(*) AS total FROM ${datasetId}`);

    const rows = Array.isArray(rowsResult.rows) ? rowsResult.rows : [];
    const totalRows = Array.isArray(countResult.rows) && countResult.rows.length > 0
      ? countResult.rows[0].total ?? countResult.rows[0].count ?? 0
      : 0;

    const output = {
      rowCount: rows.length,
      totalRows,
      firstRowKeys: rows.length > 0 ? Object.keys(rows[0]) : []
    };

    console.log(JSON.stringify(output));
  } catch (error) {
    console.error(JSON.stringify({ error: error?.message ?? String(error) }));
    process.exit(1);
  }
}

await main();
