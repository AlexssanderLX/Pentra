// Pentra execution UI: tool-parameter toggling and live log streaming (SSE).
// Served from the same origin; compatible with the strict CSP (script-src 'self',
// connect-src 'self'). No inline handlers.
(function () {
  "use strict";

  // --- Start form: show only the selected tool's parameters ---------------
  var toolSelect = document.getElementById("run-tool");
  var form = document.getElementById("start-run-form");
  if (toolSelect && form) {
    var updateParams = function () {
      var opt = toolSelect.options[toolSelect.selectedIndex];
      var slug = opt ? opt.getAttribute("data-slug") : "";
      var isActive = opt ? opt.getAttribute("data-active") === "true" : false;

      form.querySelectorAll("[data-tool-params]").forEach(function (el) {
        el.style.display = el.getAttribute("data-tool-params") === slug ? "" : "none";
      });

      var confirmBox = form.querySelector("[data-active-confirm]");
      if (confirmBox) {
        confirmBox.style.display = isActive ? "" : "none";
      }
    };
    toolSelect.addEventListener("change", updateParams);
    updateParams();
  }

  // --- Run detail: stream logs + status via SSE ---------------------------
  var consoleEl = document.getElementById("run-console");
  if (consoleEl && consoleEl.getAttribute("data-active") === "true") {
    var runId = consoleEl.getAttribute("data-run-id");
    var lastSeq = parseInt(consoleEl.getAttribute("data-last-seq") || "0", 10);
    var statusEl = document.querySelector("[data-run-status]");
    var terminal = ["Completed", "Failed", "Cancelled", "TimedOut"];

    var es = new EventSource("/Executions/Stream/" + encodeURIComponent(runId) + "?afterSeq=" + lastSeq);

    es.addEventListener("log", function (e) {
      try {
        var d = JSON.parse(e.data);
        var prefix = d.stream === "Stderr" ? "[err] " : d.stream === "System" ? "[sys] " : "";
        consoleEl.textContent += prefix + d.text + "\n";
        consoleEl.scrollTop = consoleEl.scrollHeight;
      } catch (_) { /* ignore malformed line */ }
    });

    es.addEventListener("status", function (e) {
      try {
        var d = JSON.parse(e.data);
        if (statusEl) { statusEl.textContent = d.status; }
        if (terminal.indexOf(d.status) >= 0) {
          es.close();
          // Reload once to render the structured results and final metadata.
          setTimeout(function () { window.location.reload(); }, 800);
        }
      } catch (_) { /* ignore */ }
    });

    es.onerror = function () { es.close(); };
  }
})();
