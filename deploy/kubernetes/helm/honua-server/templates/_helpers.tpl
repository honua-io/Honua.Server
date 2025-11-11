{{/*
Expand the name of the chart.
*/}}
{{- define "honua-server.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "honua-server.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "honua-server.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "honua-server.labels" -}}
helm.sh/chart: {{ include "honua-server.chart" . }}
{{ include "honua-server.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "honua-server.selectorLabels" -}}
app.kubernetes.io/name: {{ include "honua-server.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "honua-server.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "honua-server.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Create image name
*/}}
{{- define "honua-server.image" -}}
{{- $registry := .Values.image.registry }}
{{- $repository := .Values.image.repository }}
{{- $variant := .Values.image.variant }}
{{- $tag := .Values.image.tag | default .Chart.AppVersion }}
{{- if eq $variant "lite" }}
{{- printf "%s/%s:%s-lite" $registry $repository $tag }}
{{- else }}
{{- printf "%s/%s:%s" $registry $repository $tag }}
{{- end }}
{{- end }}

{{/*
Database connection string
*/}}
{{- define "honua-server.databaseHost" -}}
{{- if .Values.database.external }}
{{- .Values.database.host }}
{{- else if .Values.postgresql.enabled }}
{{- printf "%s-postgresql" .Release.Name }}
{{- else }}
{{- "localhost" }}
{{- end }}
{{- end }}

{{/*
Database port
*/}}
{{- define "honua-server.databasePort" -}}
{{- if .Values.database.external }}
{{- .Values.database.port }}
{{- else }}
{{- 5432 }}
{{- end }}
{{- end }}

{{/*
Redis host
*/}}
{{- define "honua-server.redisHost" -}}
{{- if .Values.redis.external }}
{{- .Values.redis.host }}
{{- else if .Values.redis.enabled }}
{{- printf "%s-redis-master" .Release.Name }}
{{- else }}
{{- "localhost" }}
{{- end }}
{{- end }}

{{/*
Redis port
*/}}
{{- define "honua-server.redisPort" -}}
{{- if .Values.redis.external }}
{{- .Values.redis.port }}
{{- else }}
{{- 6379 }}
{{- end }}
{{- end }}

{{/*
Database secret name
*/}}
{{- define "honua-server.databaseSecretName" -}}
{{- if .Values.database.existingSecret }}
{{- .Values.database.existingSecret }}
{{- else }}
{{- printf "%s-db-secret" (include "honua-server.fullname" .) }}
{{- end }}
{{- end }}

{{/*
Redis secret name
*/}}
{{- define "honua-server.redisSecretName" -}}
{{- if .Values.redis.existingSecret }}
{{- .Values.redis.existingSecret }}
{{- else if .Values.redis.enabled }}
{{- printf "%s-redis" .Release.Name }}
{{- else }}
{{- printf "%s-redis-secret" (include "honua-server.fullname" .) }}
{{- end }}
{{- end }}
