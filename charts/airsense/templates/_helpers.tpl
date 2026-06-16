{{- define "airsense.labels" -}}
app.kubernetes.io/name: {{ .Chart.Name }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version | replace "+" "_" }}
{{- end }}

{{- define "airsense.selectorLabels" -}}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{- define "airsense.backendImage" -}}
{{ .Values.backend.image.repository }}:{{ .Values.backend.image.tag }}
{{- end }}

{{- define "airsense.postgresConnectionString" -}}
{{- if .Values.secret.postgresConnectionString -}}
{{ .Values.secret.postgresConnectionString }}
{{- else -}}
Host=postgres;Port={{ .Values.postgres.service.port }};Database={{ .Values.secret.postgresDatabase }};Username={{ .Values.secret.postgresUser }};Password={{ .Values.secret.postgresPassword }}
{{- end -}}
{{- end }}
