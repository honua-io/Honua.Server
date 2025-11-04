## Honua Build & Packaging Strategy

### Overview

Honua ships as an open-source monolith while offering premium build profiles under a “Pro” plan.  The same codebase produces multiple bundles through MSBuild build profiles and a GitHub Actions matrix:

| Profile   | Description                                   | Audience |
|-----------|-----------------------------------------------|----------|
| Monolith  | Full OSS build (OGC, STAC, WMS, GDAL, Skia, Swagger, OData). | Community |
| Serverless | Trimmed/AOT-ready build; Swagger/OData (and optionally GDAL/Skia) disabled; ideal for cold-start sensitive deployments. | Pro |
| OgcOnly   | Microservice for OGC APIs + attachments.       | Pro |
| StacOnly  | STAC/catalog-only microservice.                | Pro |

### Build Profiles

Use MSBuild properties to control feature inclusion. Example snippet for `Honua.Server.Host.csproj`:

```xml
<PropertyGroup Condition="'$(HonuaPlatform)' == 'Serverless'">
  <DefineConstants>$(DefineConstants);EXCLUDE_SWAGGER;EXCLUDE_ODATA;EXCLUDE_LEGEND</DefineConstants>
  <PublishTrimmed>true</PublishTrimmed>
  <PublishAot>false</PublishAot>
</PropertyGroup>
```

Wrap optional registrations in conditional compilation:

```csharp
#if !EXCLUDE_SWAGGER
builder.Services.AddHonuaApiDocumentation();
#endif
```

### Docker Matrix

Single Dockerfile with build arguments:

```dockerfile
ARG BUNDLE=monolith
ARG PUBLISH_TRIMMED=false
ARG PUBLISH_AOT=false

RUN dotnet publish src/Honua.Server.Host/Honua.Server.Host.csproj \
    -c Release \
    -p:HonuaPlatform=${BUNDLE} \
    -p:PublishTrimmed=${PUBLISH_TRIMMED} \
    -p:PublishAot=${PUBLISH_AOT} \
    -p:UseAppHost=false \
    -o /app/publish
```

GitHub Actions matrix excerpt:

```yaml
strategy:
  matrix:
    bundle: [monolith, ogc-micro, stac-micro, serverless]
    arch: [linux/amd64, linux/arm64]
    include:
      - bundle: serverless
        publish_trimmed: true
        publish_aot: false
```

- Publish OSS images (monolith) to the public registry.  
- Publish Pro images (serverless, microservices) to a private registry.
- Optional: aggregate multi-arch manifests after builds.

### Trimming & AOT Considerations

1. **Swagger filters** use reflection → disable in trimmed builds or annotate with `[DynamicallyAccessedMembers]`.
2. **OData** is inherently dynamic → exclude from trimmed/AOT profiles.
3. **JSON serialization** → extend source-generation contexts to cover STAC, catalog, OGC DTOs; register combined `JsonTypeInfoResolver`.
4. **Configuration binding** → annotate options (`CacheHeaderOptions`, `VectorTilePreseedLimits`, etc.) or migrate to source-generated binding.
5. **Tests** → add smoke tests for trimmed publish:
   ```bash
   dotnet publish ... -p:HonuaPlatform=Serverless -p:PublishTrimmed=true -o artifacts/serverless
   dotnet artifacts/serverless/Honua.Server.Host.dll --urls http://localhost:5000 &
   curl -f http://localhost:5000/healthz/live
   kill %1
   ```

### GDAL & Skia on Alpine

- Install via `apk add gdal proj geos sqlite postgresql-client`.
- Set `GDAL_DATA=/usr/share/gdal`, `PROJ_LIB=/usr/share/proj`.
- SkiaSharp requires `libc6-compat`, `libstdc++`, `libgcc`, `fontconfig`, `freetype`, `libpng`, `libwebp`, `harfbuzz`, and a font package (e.g., `ttf-dejavu`).
- Expect Alpine image size ~240 MB with GDAL + Skia.  
- For trimmed profiles that do not need raster rendering, exclude Skia/GDAL to reduce footprint.

### Licensing & Distribution

- OSS monolith remains public (`honua/monolith:<version>`).
- Pro builds (serverless, microservices) are published to a private registry.
- Provide documentation or a CLI helper to authenticate and pull pro images.

### Workflow Summary

1. OSS and Pro bundles built via matrix.  
2. Trimmed builds validated with smoke tests.  
3. Multi-arch images optionally aggregated.  
4. Documentation clearly communicates feature availability per bundle.  
5. Future AOT work adds annotations and removes reflection blockers, enabling `PublishAot=true` in the serverless profile.  
6. Security scanning and SBOM generation apply to both OSS and Pro images.
