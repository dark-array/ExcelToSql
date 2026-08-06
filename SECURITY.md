# Security Policy

[English](#english) | [Español](#español)

## English

ExcelToSql is a **local** tool. It is not meant to be exposed on the public internet. Anyone who can reach the app can upload files and generate SQL.

### Supported versions

Only the latest code on the default branch is supported.

### How to report a vulnerability

Do **not** open a public GitHub issue.

1. Open a private advisory: GitHub repo → **Security** → **Report a vulnerability**
2. Include steps to reproduce, the impact, and whether it needs a file upload

Please wait for a response before publishing details. If the report is valid, the fix will go out in a regular commit and the advisory can be disclosed afterwards.

Typical issues worth reporting: path traversal on uploads, unexpected script execution, or leaking data between sessions.

---

## Español

ExcelToSql es una herramienta **local**. No está pensada para exponerla en Internet. Quien alcance la URL puede subir archivos y generar SQL.

### Versiones soportadas

Solo el código actual de la rama por defecto.

### Cómo reportar una vulnerabilidad

**No** abras un issue público.

1. Abre un aviso privado: repositorio en GitHub → **Security** → **Report a vulnerability**
2. Incluye pasos para reproducir, el impacto y si hace falta subir un archivo

Espera respuesta antes de publicar detalles. Si el reporte es válido, el arreglo sale en un commit normal y el aviso se puede divulgar después.

Reporta sobre todo: path traversal en la subida, ejecución inesperada de scripts o filtrado de datos entre sesiones.
