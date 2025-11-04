# Data Seeder

DataSeeder seeds the sample OGC fixtures into both providers so local testing stays aligned.

## Usage

`
dotnet run --project tools/DataSeeder -- --provider postgres
`

Options:

- --provider <postgres|sqlite|both>: target provider(s); defaults to both.
- --postgres-connection <conn>: override the Postgres connection string (defaults to Host=localhost;Database=honua;Username=honua;Password=secret).
- --sqlite-path <path>: path to the SQLite database (defaults to samples/ogc/ogc-sample.db).

The PostgreSQL seeding path performs upserts against public.roads_primary and expects PostGIS to be enabled. SQLite seeding updates the SpatiaLite sample database in samples/ogc.
