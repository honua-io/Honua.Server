# Third-Party Licenses

HonuaIO uses the following open source and third-party software components. We are grateful to the developers and contributors of these projects.

## Table of Contents

- [MIT License](#mit-license)
- [Apache 2.0 License](#apache-20-license)
- [BSD-3-Clause License](#bsd-3-clause-license)
- [PostgreSQL License](#postgresql-license)
- [Oracle Free Use Terms and Conditions](#oracle-free-use-terms-and-conditions)

---

## MIT License

The following components are licensed under the MIT License:

### Microsoft Packages

- **Microsoft.Extensions.*** (all Microsoft.Extensions packages)
- **Microsoft.AspNetCore.*** (all ASP.NET Core packages)
- **Microsoft.Azure.*** (all Azure SDK packages)
- **Microsoft.Data.Sqlite**
- **Microsoft.SemanticKernel** and related packages
- **Microsoft.Identity.Client**
- **Microsoft.OData.Core**, **Microsoft.OData.Edm**, **Microsoft.Spatial**

**Copyright**: Microsoft Corporation
**License**: MIT
**Website**: https://github.com/dotnet

### Azure Packages

- **Azure.AI.OpenAI**
- **Azure.Identity**
- **Azure.Messaging.EventGrid**
- **Azure.Monitor.OpenTelemetry.Exporter**
- **Azure.ResourceManager** and related packages
- **Azure.Search.Documents**
- **Azure.Security.KeyVault.Certificates**
- **Azure.Storage.Blobs**
- **Azure.Extensions.AspNetCore.DataProtection.Keys**

**Copyright**: Microsoft Corporation
**License**: MIT
**Website**: https://github.com/Azure/azure-sdk-for-net

### GDAL Wrapper

- **MaxRev.Gdal.Core**
- **MaxRev.Gdal.WindowsRuntime.Minimal**
- **MaxRev.Gdal.LinuxRuntime.Minimal**
- **MaxRev.Gdal.OSXRuntime.Minimal**

**Copyright**: MaxRev
**License**: MIT
**Website**: https://github.com/MaxRev-Dev/gdal.netcore

### Database Drivers

- **MySqlConnector**

**Copyright**: MySqlConnector Contributors
**License**: MIT
**Website**: https://github.com/mysql-net/MySqlConnector

### Other MIT Licensed Components

- **LibGit2Sharp**
  Copyright: LibGit2Sharp Contributors
  https://github.com/libgit2/libgit2sharp

- **SkiaSharp**
  Copyright: Microsoft Corporation (originally Mono project)
  https://github.com/mono/SkiaSharp

- **Newtonsoft.Json**
  Copyright: James Newton-King
  https://www.newtonsoft.com/json

- **YamlDotNet**
  Copyright: Antoine Aubry and contributors
  https://github.com/aaubry/YamlDotNet

- **Spectre.Console**
  Copyright: Patrik Svensson, Phil Scott, Nils Andresen
  https://github.com/spectreconsole/spectre.console

- **Anthropic.SDK**
  Copyright: Anthropic SDK Contributors
  https://github.com/anthropics/anthropic-sdk-dotnet

- **DnsClient**
  Copyright: DnsClient.NET Contributors
  https://github.com/MichaCo/DnsClient.NET

- **Humanizer.Core**
  Copyright: Humanizer Contributors
  https://github.com/Humanizr/Humanizer

- **Konscious.Security.Cryptography.Argon2**
  Copyright: Konscious Contributors
  https://github.com/kmaragon/Konscious.Security.Cryptography

### MIT License Text

```
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## Apache 2.0 License

The following components are licensed under the Apache License 2.0:

### AWS SDK

- **AWSSDK.Core**
- **AWSSDK.ECR**
- **AWSSDK.Extensions.NETCore.Setup**
- **AWSSDK.IdentityManagement**
- **AWSSDK.KeyManagementService**
- **AWSSDK.Redshift**
- **AWSSDK.RedshiftDataAPIService**
- **AWSSDK.Route53**
- **AWSSDK.S3**
- **AWSSDK.SimpleNotificationService**

**Copyright**: Amazon.com, Inc. or its affiliates
**License**: Apache 2.0
**Website**: https://github.com/aws/aws-sdk-net

### Google Cloud SDK

- **Google.Apis.CloudResourceManager.v1**
- **Google.Cloud.ArtifactRegistry.V1**
- **Google.Cloud.BigQuery.V2**
- **Google.Cloud.Iam.Admin.V1**
- **Google.Cloud.Iam.V1**
- **Google.Cloud.Kms.V1**
- **Google.Cloud.Storage.V1**

**Copyright**: Google LLC
**License**: Apache 2.0
**Website**: https://github.com/googleapis/google-cloud-dotnet

### Database Drivers & Data Access

- **Dapper**
  Copyright: DapperLib Contributors
  https://github.com/DapperLib/Dapper

- **MongoDB.Driver**
  Copyright: MongoDB, Inc.
  https://github.com/mongodb/mongo-csharp-driver

- **Snowflake.Data**
  Copyright: Snowflake Computing, Inc.
  https://github.com/snowflakedb/snowflake-connector-net

- **ParquetSharp**
  Copyright: G-Research
  https://github.com/G-Research/ParquetSharp

### Logging & Observability

- **Serilog** and related packages:
  - Serilog.AspNetCore
  - Serilog.Enrichers.Environment
  - Serilog.Enrichers.Thread
  - Serilog.Formatting.Compact
  - Serilog.Settings.Configuration
  - Serilog.Sinks.Console
  - Serilog.Sinks.File
  - Serilog.Sinks.Seq

**Copyright**: Serilog Contributors
**License**: Apache 2.0
**Website**: https://serilog.net/

- **OpenTelemetry** and related packages:
  - OpenTelemetry
  - OpenTelemetry.Api
  - OpenTelemetry.Exporter.Console
  - OpenTelemetry.Exporter.OpenTelemetryProtocol
  - OpenTelemetry.Exporter.Prometheus.AspNetCore
  - OpenTelemetry.Extensions.Hosting
  - OpenTelemetry.Instrumentation.AspNetCore
  - OpenTelemetry.Instrumentation.Http
  - OpenTelemetry.Instrumentation.Runtime
  - OpenTelemetry.Instrumentation.SqlClient
  - OpenTelemetry.Instrumentation.StackExchangeRedis

**Copyright**: OpenTelemetry Authors
**License**: Apache 2.0
**Website**: https://github.com/open-telemetry/opentelemetry-dotnet

### Other Apache 2.0 Components

- **FluentValidation**
  Copyright: Jeremy Skinner and contributors
  https://github.com/FluentValidation/FluentValidation

- **FlatGeobuf**
  Copyright: FlatGeobuf Contributors
  https://github.com/flatgeobuf/flatgeobuf

- **Apache.Arrow**
  Copyright: The Apache Software Foundation
  https://github.com/apache/arrow

- **JsonSchema.Net**
  Copyright: JsonSchema.Net Contributors
  https://github.com/gregsdennis/json-everything

### Apache 2.0 License Text

```
Apache License
Version 2.0, January 2004
http://www.apache.org/licenses/

TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

[Full Apache 2.0 license text available at: https://www.apache.org/licenses/LICENSE-2.0.txt]
```

---

## BSD-3-Clause License

The following components are licensed under the BSD-3-Clause License:

### NetTopologySuite (Critical GIS Library)

- **NetTopologySuite**
- **NetTopologySuite.IO.GeoJSON**
- **NetTopologySuite.IO.GeoPackage**
- **NetTopologySuite.IO.ShapeFile**
- **NetTopologySuite.IO.VectorTiles**
- **NetTopologySuite.IO.VectorTiles.Mapbox**

**Copyright**: 2006-2024 NetTopologySuite Team
**License**: BSD-3-Clause
**Website**: https://github.com/NetTopologySuite/NetTopologySuite

### Resilience Library

- **Polly**

**Copyright**: 2015-2024 App vNext
**License**: BSD-3-Clause
**Website**: https://github.com/App-vNext/Polly

### Image Processing

- **BitMiracle.LibTiff.NET**

**Copyright**: Bit Miracle
**License**: BSD-3-Clause (New BSD License)
**Website**: https://github.com/BitMiracle/libtiff.net

### Projection Library

- **ProjNET**

**Copyright**: ProjNET Contributors
**License**: BSD-3-Clause
**Website**: https://github.com/NetTopologySuite/ProjNet4GeoAPI

### BSD-3-Clause License Text

```
BSD 3-Clause License

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

---

## PostgreSQL License

The following components are licensed under the PostgreSQL License (similar to MIT/BSD):

### Database Drivers

- **Npgsql**
- **Npgsql.NetTopologySuite**

**Copyright**: The Npgsql Development Team
**License**: PostgreSQL License (MIT-like)
**Website**: https://github.com/npgsql/npgsql

### PostgreSQL License Text

```
PostgreSQL License

Permission to use, copy, modify, and distribute this software and its
documentation for any purpose, without fee, and without a written agreement
is hereby granted, provided that the above copyright notice and this
paragraph and the following two paragraphs appear in all copies.

IN NO EVENT SHALL THE NPGSQL DEVELOPMENT TEAM BE LIABLE TO ANY PARTY FOR
DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES, INCLUDING
LOST PROFITS, ARISING OUT OF THE USE OF THIS SOFTWARE AND ITS
DOCUMENTATION, EVEN IF THE NPGSQL DEVELOPMENT TEAM HAS BEEN ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.

THE NPGSQL DEVELOPMENT TEAM SPECIFICALLY DISCLAIMS ANY WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
AND FITNESS FOR A PARTICULAR PURPOSE. THE SOFTWARE PROVIDED HEREUNDER IS
ON AN "AS IS" BASIS, AND THE NPGSQL DEVELOPMENT TEAM HAS NO OBLIGATIONS TO
PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.
```

---

## Oracle Free Use Terms and Conditions

### Oracle Data Provider

- **Oracle.ManagedDataAccess.Core**

**Copyright**: Oracle Corporation
**License**: Oracle Free Use Terms and Conditions (FUTC)
**Website**: https://www.oracle.com/database/technologies/appdev/dotnet/odp.html

**Summary**: Oracle provides ODP.NET under free use terms that allow:
- Development, testing, and production use
- Redistribution of unmodified programs
- Commercial use without additional fees to Oracle
- Cloud hosting and deployment

**Full License**: https://www.oracle.com/downloads/licenses/oracle-free-license.html

---

## Additional Components

The following components have specific licenses:

### Data Processing

- **StackExchange.Redis**
  Copyright: Stack Exchange, Inc.
  License: MIT
  https://github.com/StackExchange/StackExchange.Redis

- **K4os.Compression.LZ4**
  Copyright: Milosz Krajewski
  License: MIT
  https://github.com/MiloszKrajewski/K4os.Compression.LZ4

- **ZstdSharp.Port**
  Copyright: Oleg Stepanischev
  License: BSD-3-Clause
  https://github.com/oleg-st/ZstdSharp

### Cloud & Infrastructure

- **Certes** (ACME/Let's Encrypt client)
  Copyright: Certes Contributors
  License: MIT
  https://github.com/fszlin/certes

- **CloudFlare.Client**
  Copyright: CloudFlare SDK Contributors
  License: MIT
  https://github.com/zingz0r/CloudFlare.Client

- **Yarp.ReverseProxy**
  Copyright: Microsoft Corporation
  License: MIT
  https://github.com/microsoft/reverse-proxy

### UI & Visualization

- **Swashbuckle.AspNetCore**
  Copyright: Swashbuckle Contributors
  License: MIT
  https://github.com/domaindrivendev/Swashbuckle.AspNetCore

- **Blazorise.Bootstrap5**, **Blazorise.Charts**, **Blazorise.Icons.FontAwesome**
  Copyright: Megabit d.o.o.
  License: Commercial (Free for non-commercial use)
  https://blazorise.com/

- **SharpKml.Core**
  Copyright: SharpKML Contributors
  License: BSD-3-Clause
  https://github.com/samcragg/sharpkml

### Version Management

- **Asp.Versioning.Mvc**, **Asp.Versioning.Mvc.ApiExplorer**
  Copyright: Microsoft Corporation
  License: MIT
  https://github.com/dotnet/aspnet-api-versioning

### Health Checks

- **AspNetCore.HealthChecks.*** (all health check packages)
  Copyright: Xabaril Contributors
  License: Apache 2.0
  https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks

### Source Control

- **Octokit**
  Copyright: GitHub, Inc.
  License: MIT
  https://github.com/octokit/octokit.net

### Dependency Injection

- **Scrutor**
  Copyright: Kristian Hellang
  License: MIT
  https://github.com/khellang/Scrutor

---

## Notes

1. **No GPL or LGPL dependencies**: This project does not depend on any GPL or LGPL licensed components that would require derivative works to be open-sourced.

2. **Oracle FUTC Compliance**: The use of Oracle.ManagedDataAccess.Core complies with Oracle's free use terms. We redistribute the unmodified Oracle driver and do not charge additional fees for it.

3. **Attribution**: This file serves as attribution for all permissive licenses (MIT, Apache 2.0, BSD) that require acknowledgment of copyright holders.

4. **Full License Texts**: Full license texts for each component can be found in their respective NuGet packages or GitHub repositories.

5. **Enterprise Components**: Additional enterprise data providers (BigQuery, Cosmos DB, MongoDB, Redshift, Snowflake) use Apache 2.0 or permissive licenses compatible with commercial use.

---

**Last Updated**: January 2025

For questions about licensing, contact: legal@honua.io
