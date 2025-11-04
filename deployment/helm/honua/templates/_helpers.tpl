{{/*
Expand the name of the chart.
*/}}
{{- define "honua.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "honua.fullname" -}}
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
{{- define "honua.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "honua.labels" -}}
helm.sh/chart: {{ include "honua.chart" . }}
{{ include "honua.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- with .Values.commonLabels }}
{{ toYaml . }}
{{- end }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "honua.selectorLabels" -}}
app.kubernetes.io/name: {{ include "honua.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
API component labels
*/}}
{{- define "honua.api.labels" -}}
{{ include "honua.labels" . }}
app.kubernetes.io/component: api
{{- end }}

{{/*
API selector labels
*/}}
{{- define "honua.api.selectorLabels" -}}
{{ include "honua.selectorLabels" . }}
app.kubernetes.io/component: api
{{- end }}

{{/*
Intake component labels
*/}}
{{- define "honua.intake.labels" -}}
{{ include "honua.labels" . }}
app.kubernetes.io/component: intake
{{- end }}

{{/*
Intake selector labels
*/}}
{{- define "honua.intake.selectorLabels" -}}
{{ include "honua.selectorLabels" . }}
app.kubernetes.io/component: intake
{{- end }}

{{/*
Orchestrator component labels
*/}}
{{- define "honua.orchestrator.labels" -}}
{{ include "honua.labels" . }}
app.kubernetes.io/component: orchestrator
{{- end }}

{{/*
Orchestrator selector labels
*/}}
{{- define "honua.orchestrator.selectorLabels" -}}
{{ include "honua.selectorLabels" . }}
app.kubernetes.io/component: orchestrator
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "honua.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "honua.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
API service account name
*/}}
{{- define "honua.api.serviceAccountName" -}}
{{- printf "%s-api" (include "honua.fullname" .) }}
{{- end }}

{{/*
Intake service account name
*/}}
{{- define "honua.intake.serviceAccountName" -}}
{{- printf "%s-intake" (include "honua.fullname" .) }}
{{- end }}

{{/*
Orchestrator service account name
*/}}
{{- define "honua.orchestrator.serviceAccountName" -}}
{{- printf "%s-orchestrator" (include "honua.fullname" .) }}
{{- end }}

{{/*
Image pull secrets
*/}}
{{- define "honua.imagePullSecrets" -}}
{{- if .Values.global.imagePullSecrets }}
imagePullSecrets:
{{- range .Values.global.imagePullSecrets }}
  - name: {{ . }}
{{- end }}
{{- else if .Values.image.pullSecrets }}
imagePullSecrets:
{{- range .Values.image.pullSecrets }}
  - name: {{ . }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Return the proper image name
*/}}
{{- define "honua.api.image" -}}
{{- $registry := default .Values.image.registry .Values.global.imageRegistry }}
{{- $repository := .Values.api.image.repository }}
{{- $tag := default .Chart.AppVersion .Values.api.image.tag }}
{{- if $registry }}
{{- printf "%s/%s:%s" $registry $repository $tag }}
{{- else }}
{{- printf "%s:%s" $repository $tag }}
{{- end }}
{{- end }}

{{/*
Return the proper image name for intake
*/}}
{{- define "honua.intake.image" -}}
{{- $registry := default .Values.image.registry .Values.global.imageRegistry }}
{{- $repository := .Values.intake.image.repository }}
{{- $tag := default .Chart.AppVersion .Values.intake.image.tag }}
{{- if $registry }}
{{- printf "%s/%s:%s" $registry $repository $tag }}
{{- else }}
{{- printf "%s:%s" $repository $tag }}
{{- end }}
{{- end }}

{{/*
Return the proper image name for orchestrator
*/}}
{{- define "honua.orchestrator.image" -}}
{{- $registry := default .Values.image.registry .Values.global.imageRegistry }}
{{- $repository := .Values.orchestrator.image.repository }}
{{- $tag := default .Chart.AppVersion .Values.orchestrator.image.tag }}
{{- if $registry }}
{{- printf "%s/%s:%s" $registry $repository $tag }}
{{- else }}
{{- printf "%s:%s" $repository $tag }}
{{- end }}
{{- end }}

{{/*
PostgreSQL host
*/}}
{{- define "honua.postgresql.host" -}}
{{- if .Values.postgresql.enabled }}
{{- printf "%s-postgresql" (include "honua.fullname" .) }}
{{- else }}
{{- required "postgresql.externalHost is required when postgresql.enabled is false" .Values.postgresql.externalHost }}
{{- end }}
{{- end }}

{{/*
Redis host
*/}}
{{- define "honua.redis.host" -}}
{{- if .Values.redis.enabled }}
{{- printf "%s-redis-master" (include "honua.fullname" .) }}
{{- else }}
{{- required "redis.externalHost is required when redis.enabled is false" .Values.redis.externalHost }}
{{- end }}
{{- end }}

{{/*
Storage class
*/}}
{{- define "honua.storageClass" -}}
{{- if .Values.global.storageClass }}
{{- .Values.global.storageClass }}
{{- else }}
{{- "standard" }}
{{- end }}
{{- end }}
