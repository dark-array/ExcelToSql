(() => {
  const dropzone = document.getElementById("dropzone");
  const fileInput = dropzone?.querySelector("input[type=file]");
  const fileName = document.getElementById("file-name");

  if (dropzone && fileInput) {
    const showName = () => {
      if (fileName) {
        fileName.textContent = fileInput.files?.[0]?.name || "";
      }
    };

    fileInput.addEventListener("change", showName);
    ["dragenter", "dragover"].forEach((eventName) => {
      dropzone.addEventListener(eventName, (event) => {
        event.preventDefault();
        dropzone.classList.add("drag");
      });
    });
    ["dragleave", "drop"].forEach((eventName) => {
      dropzone.addEventListener(eventName, (event) => {
        event.preventDefault();
        dropzone.classList.remove("drag");
      });
    });
    dropzone.addEventListener("drop", (event) => {
      const files = event.dataTransfer?.files;
      if (files?.length) {
        fileInput.files = files;
        showName();
      }
    });
  }

  document.querySelectorAll(".toggle-cols").forEach((button) => {
    button.addEventListener("click", () => {
      const card = button.closest(".sheet-card");
      const checks = [...(card?.querySelectorAll(".col-check") ?? [])];
      const allOn = checks.length > 0 && checks.every((item) => item.checked);
      checks.forEach((item) => {
        item.checked = !allOn;
      });
    });
  });

  document.querySelectorAll("select[name='Editor.IdentityColumn']").forEach((select) => {
    select.addEventListener("change", () => {
      if (!select.value) return;
      const sheetRow = select.closest(".sheet-card");
      sheetRow?.querySelectorAll(".col-check").forEach((check) => {
        const nameInput = check.parentElement?.querySelector("input[type=hidden][name$='.OriginalName']");
        if (nameInput?.value === select.value) check.checked = true;
      });
    });
  });

  const copyButton = document.getElementById("copy-sql");
  const sqlBox = document.getElementById("sql-output");
  if (copyButton && sqlBox) {
    copyButton.addEventListener("click", async () => {
      try {
        await navigator.clipboard.writeText(sqlBox.textContent || "");
        copyButton.textContent = "Copiado";
        setTimeout(() => {
          copyButton.textContent = "Copiar";
        }, 1500);
      } catch {
        copyButton.textContent = "No se pudo copiar";
      }
    });
  }

  const previewNode = document.getElementById("preview-data");
  const modal = document.getElementById("data-modal");
  if (previewNode && modal) {
    let sheetData = {};
    try {
      sheetData = JSON.parse(previewNode.textContent || "{}");
    } catch {
      sheetData = {};
    }

    const title = document.getElementById("data-modal-title");
    const meta = document.getElementById("data-modal-meta");
    const table = document.getElementById("data-modal-table");
    const close = document.getElementById("data-modal-close");

    const isOn = (el) => {
      if (!el) return false;
      if (el.type === "checkbox") return el.checked;
      return el.value === "true" || el.value === "True";
    };

    const escapeHtml = (value) =>
      String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;");

    const parseStrip = (pattern) => {
      const chars = new Set();
      const tokens = [];
      if (!pattern) return { chars, tokens };

      let work = pattern;
      work = work.replace(/"([^"]+)"/g, (_, token) => {
        tokens.push(token);
        return " ";
      });

      const brackets = [...work.matchAll(/\[(.*?)\]/g)];
      if (brackets.length > 0) {
        brackets.forEach((match) => {
          chars.add("[");
          chars.add("]");
          [...match[1]].forEach((ch) => chars.add(ch));
        });
        return { chars, tokens };
      }

      [...work].forEach((ch) => chars.add(ch));
      return { chars, tokens };
    };

    const applyClean = (value, pattern) => {
      if (!value) return "";
      if (!pattern || !pattern.trim()) return value;
      const { chars, tokens } = parseStrip(pattern);
      let result = value;
      tokens.forEach((token) => {
        result = result.split(token).join("");
      });
      if (chars.size === 0) return result;
      return [...result].filter((ch) => !chars.has(ch)).join("");
    };

    const readGlobalOptions = () => ({
      trim: isOn(document.querySelector("[name='Input.TrimWhitespace']")),
      skipBlank: isOn(document.querySelector("[name='Input.SkipBlankRows']"))
    });

    const readEditorColumns = () => {
      const rows = document.querySelectorAll(".sheet-card table.cols tbody tr");
      return [...rows].map((row, index) => ({
        selected: isOn(row.querySelector(".col-check")),
        originalName:
          row.querySelector("input[name$='.OriginalName']")?.value || "",
        sqlName: row.querySelector("input[name$='.SqlName']")?.value || "",
        stripChars: row.querySelector("input[name$='.StripChars']")?.value || "",
        headerIndex: index
      }));
    };

    const readSavedColumns = (sheetIndex) => {
      const cols = [];
      for (let i = 0; ; i++) {
        const prefix = `Input.Sheets[${sheetIndex}].Columns[${i}]`;
        const original = document.querySelector(`[name="${prefix}.OriginalName"]`);
        if (!original) break;
        cols.push({
          selected: isOn(document.querySelector(`[name="${prefix}.Selected"]`)),
          originalName: original.value || "",
          sqlName: document.querySelector(`[name="${prefix}.SqlName"]`)?.value || "",
          stripChars: document.querySelector(`[name="${prefix}.StripChars"]`)?.value || ""
        });
      }
      return cols;
    };

    const renderPreview = (sheetName, columns) => {
      const data = sheetData[sheetName];
      const selected = columns.filter((col) => col.selected);
      if (!data) {
        title.textContent = sheetName;
        meta.textContent = "No hay datos de esa hoja.";
        table.innerHTML = "";
        modal.hidden = false;
        return;
      }
      if (selected.length === 0) {
        title.textContent = sheetName;
        meta.textContent = "Seleccione al menos una columna.";
        table.innerHTML = "";
        modal.hidden = false;
        return;
      }

      const options = readGlobalOptions();
      const headerIndex = (col) => {
        if (Number.isInteger(col.headerIndex)) return col.headerIndex;
        return data.headers.findIndex(
          (name) => name.toLowerCase() === col.originalName.toLowerCase()
        );
      };

      const rows = [];
      data.rows.forEach((row) => {
        const values = selected.map((col) => {
          const idx = headerIndex(col);
          let raw = idx >= 0 && idx < row.length ? row[idx] ?? "" : "";
          raw = applyClean(raw, col.stripChars);
          if (options.trim) raw = raw.trim();
          return raw;
        });
        if (options.skipBlank && values.every((cell) => !cell.trim())) return;
        rows.push(values);
      });

      title.textContent = sheetName;
      meta.textContent = `${selected.length} de ${columns.length} columnas · ${rows.length} filas visibles` +
        (data.totalRows > data.rows.length ? ` (máx. ${data.rows.length} de ${data.totalRows})` : "");

      const head = selected
        .map((col) => `<th>${escapeHtml(col.sqlName || col.originalName)}<small>${escapeHtml(col.originalName)}</small></th>`)
        .join("");
      const body = rows
        .map(
          (row) =>
            `<tr>${row
              .map((cell) => `<td>${cell ? escapeHtml(cell) : "<span class='null'>NULL</span>"}</td>`)
              .join("")}</tr>`
        )
        .join("");
      table.innerHTML = `<thead><tr>${head}</tr></thead><tbody>${body || "<tr><td colspan='" + selected.length + "'>Sin filas</td></tr>"}</tbody>`;
      modal.hidden = false;
    };

    document.getElementById("open-preview")?.addEventListener("click", () => {
      const sheetName = document.getElementById("open-preview")?.getAttribute("data-sheet");
      renderPreview(sheetName, readEditorColumns());
    });

    document.querySelectorAll(".preview-saved").forEach((button) => {
      button.addEventListener("click", () => {
        const index = Number(button.getAttribute("data-index"));
        const sheetName = button.getAttribute("data-sheet");
        renderPreview(sheetName, readSavedColumns(index));
      });
    });

    close?.addEventListener("click", () => {
      modal.hidden = true;
    });
    modal.addEventListener("click", (event) => {
      if (event.target === modal) modal.hidden = true;
    });
    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape") modal.hidden = true;
    });
  }
})();
