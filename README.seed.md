# Quick Start: Seeded Honua Instance

This guide provides the fastest way to get a fully functional Honua GIS server running with sample data.

## TL;DR

```bash
# Clone the repository
git clone https://github.com/yourusername/HonuaIO.git
cd HonuaIO

# Start seeded instance
docker-compose -f docker-compose.seed.yml up

# Access at http://localhost:8080
```

That's it! The database will be automatically populated with sample GIS data.

## What You Get

- **PostgreSQL 16 + PostGIS 3.4**: Fully configured spatial database
- **Honua Server**: Complete GIS server with all APIs enabled
- **Sample Data**: Pre-populated features, geometries, and metadata
- **Ready to Use**: All endpoints immediately accessible

## Quick Examples

### Check Server Health

```bash
curl http://localhost:8080/health
```

### Get OGC API Landing Page

```bash
curl http://localhost:8080/
```

### List Available Collections

```bash
curl http://localhost:8080/collections
```

### Query Features via WFS

```bash
curl "http://localhost:8080/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=test_features&outputFormat=application/json"
```

### Get WMS Map

```bash
curl "http://localhost:8080/wms?service=WMS&version=1.3.0&request=GetMap&layers=test_layer&width=800&height=600&crs=EPSG:4326&bbox=-180,-90,180,90&format=image/png" > map.png
```

### Browse STAC Catalog

```bash
curl http://localhost:8080/stac
```

## Configuration

Environment variables are defined in `.env.seed`:

```env
# Database
POSTGRES_USER=honua
POSTGRES_PASSWORD=honua_seed_pass
POSTGRES_DB=honua
POSTGRES_PORT=5432

# Server
HONUA_PORT=8080
ASPNETCORE_ENVIRONMENT=Development

# Authentication (QuickStart mode for easy testing)
AUTH_MODE=Local
QUICKSTART_ENABLED=true
AUTH_ENFORCE=false
```

## Available APIs

Once running, you'll have access to:

| API | Endpoint | Description |
|-----|----------|-------------|
| OGC API - Features | `http://localhost:8080/` | Modern OGC standard for feature access |
| WFS 2.0/3.0 | `http://localhost:8080/wfs` | Web Feature Service |
| WMS 1.3.0 | `http://localhost:8080/wms` | Web Map Service |
| WCS 2.0 | `http://localhost:8080/wcs` | Web Coverage Service |
| STAC 1.0 | `http://localhost:8080/stac` | SpatioTemporal Asset Catalog |
| GeoServices REST | `http://localhost:8080/rest` | Esri-compatible REST API |
| Health Checks | `http://localhost:8080/health` | Service health status |

## Management Commands

```bash
# Start in background
docker-compose -f docker-compose.seed.yml up -d

# View logs
docker-compose -f docker-compose.seed.yml logs -f

# Stop (keep data)
docker-compose -f docker-compose.seed.yml down

# Stop and remove all data
docker-compose -f docker-compose.seed.yml down -v

# Restart service
docker-compose -f docker-compose.seed.yml restart honua-server

# Rebuild and restart
docker-compose -f docker-compose.seed.yml build honua-server
docker-compose -f docker-compose.seed.yml up -d honua-server
```

## Testing with cURL

### Get All Collections

```bash
curl -s http://localhost:8080/collections | jq .
```

### Get Features as GeoJSON

```bash
curl -s "http://localhost:8080/collections/test_features/items?limit=10" | jq .
```

### Get Feature by ID

```bash
curl -s "http://localhost:8080/collections/test_features/items/1" | jq .
```

### Spatial Query (Bounding Box)

```bash
curl -s "http://localhost:8080/collections/test_features/items?bbox=-180,-90,180,90&limit=5" | jq .
```

## Testing with QGIS

1. **Open QGIS**
2. **Add WFS Layer:**
   - Layer → Add Layer → Add WFS Layer
   - Create new connection:
     - Name: `Honua Local`
     - URL: `http://localhost:8080/wfs`
   - Click "OK" and connect
3. **Add WMS Layer:**
   - Layer → Add Layer → Add WMS/WMTS Layer
   - Create new connection:
     - Name: `Honua Local WMS`
     - URL: `http://localhost:8080/wms`

## Accessing the Database

Connect directly to PostgreSQL:

```bash
# Using psql
docker exec -it honua-postgres-seed psql -U honua -d honua

# Example queries
\dt                                    # List all tables
SELECT COUNT(*) FROM test_features;    # Count features
SELECT ST_AsText(geom) FROM test_features LIMIT 1;  # View geometry
```

## Troubleshooting

### Port Already in Use

Change the port in `.env.seed`:

```env
HONUA_PORT=8081
POSTGRES_PORT=5433
```

### Services Not Starting

Check logs:

```bash
docker-compose -f docker-compose.seed.yml logs
```

### Need Fresh Start

Remove everything and start over:

```bash
docker-compose -f docker-compose.seed.yml down -v
docker-compose -f docker-compose.seed.yml up
```

## What's Next?

- Read the [full documentation](docs/docker/SEEDED_DEPLOYMENT.md)
- Explore the [API reference](docs/api/)
- Try [performance testing](tests/load/)
- Set up [production deployment](docs/DEPLOYMENT.md)

## Support

- **Documentation**: [docs/](docs/)
- **Issues**: GitHub Issues
- **Examples**: [examples/](examples/)
