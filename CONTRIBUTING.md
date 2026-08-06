# Contributing

[English](#english) | [Español](#español)

## English

Thanks for taking a look at ExcelToSql. Small, focused changes are easier to review than large rewrites.

### How to run

```bash
git clone <repo-url>
cd ExcelToSql
dotnet run
```

Open [http://localhost:5185](http://localhost:5185). Sample file: `samples/clientes.csv`.

You need the [.NET 9 SDK](https://dotnet.microsoft.com/download).

### Before you open a pull request

- `dotnet build` succeeds
- The flow you changed still works in the browser: upload → configure sheet → generate SQL
- Do not commit `bin/`, `obj/`, `.vs/`, or personal Excel files
- Keep secrets out of the repo (no connection strings, `.env`, or real customer data)

### Bugs and ideas

Use GitHub Issues. Include the file type (xlsx/csv), the engine (PostgreSQL, SQL Server, …), and what you expected vs what you got.

Security problems: see [SECURITY.md](SECURITY.md). Do not open a public issue for those.

### Code notes

This is a Razor Pages monolith. Conversion logic lives in `Services/`; keep new SQL dialects behind `ISqlDialect` and `SqlDialectFactory`.

The UI is in Spanish. If you add labels, keep that language unless the change is documentation.

---

## Español

Gracias por revisar ExcelToSql. Los cambios chicos y concretos son más fáciles de revisar que un rewrite grande.

### Cómo ejecutar

```bash
git clone <url-del-repo>
cd ExcelToSql
dotnet run
```

Abre [http://localhost:5185](http://localhost:5185). Archivo de ejemplo: `samples/clientes.csv`.

Hace falta el [.NET 9 SDK](https://dotnet.microsoft.com/download).

### Antes de abrir un pull request

- `dotnet build` termina bien
- El flujo que tocaste sigue funcionando en el navegador: subir → configurar hoja → generar SQL
- No subas `bin/`, `obj/`, `.vs/` ni Excels personales
- Nada de secretos en el repo (connection strings, `.env` o datos reales de clientes)

### Bugs e ideas

Usa GitHub Issues. Incluye el tipo de archivo (xlsx/csv), el motor y qué esperabas vs qué obtuviste.

Problemas de seguridad: ver [SECURITY.md](SECURITY.md). No los publiques en un issue abierto.

### Notas de código

Es un monolito de Razor Pages. La conversión está en `Services/`; un motor nuevo va por `ISqlDialect` y `SqlDialectFactory`.

La UI está en español. Si agregas textos en la app, mantén ese idioma (la documentación sí puede ir en ambos).
