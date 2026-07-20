// app-shell.js — le pont côté page : chaque ui/*.html l'inclut via
// <script src="sg/app-shell.js"></script> (copié là par build-app.sh).
// SG.call('cmd', args) -> Promise ; les commandes vivent dans host.js.

window.SG = (function () {
  var seq = 0, pending = {}, dropHandlers = [], pushHandlers = {};

  function call(cmd, args) {
    return new Promise(function (resolve) {
      var id = ++seq;
      pending[id] = resolve;
      window.webkit.messageHandlers.stargazer.postMessage(
        JSON.stringify({ id: id, cmd: cmd, args: args || {} }));
    });
  }

  var SG = {
    call: call,
    __resolve: function (id, json) {
      var r = pending[id];
      delete pending[id];
      if (r) r(JSON.parse(json));
    },
    __drop: function (json) {
      var paths = JSON.parse(json);
      dropHandlers.forEach(function (h) { h(paths); });
    },
    // le natif peut pousser des événements : SG.__push('nom', payload)
    __push: function (name, json) {
      (pushHandlers[name] || []).forEach(function (h) {
        h(JSON.parse(json));
      });
    },
    onDrop: function (h) { dropHandlers.push(h); },
    on: function (name, h) {
      (pushHandlers[name] = pushHandlers[name] || []).push(h);
    },

    // ------------------------------------------------ raccourcis usuels
    env: function () { return call('env'); },
    shell: function (script) { return call('shell', { script: script }); },
    read: function (p) { return call('read', { path: p }); },
    write: function (p, t) { return call('write', { path: p, text: t }); },
    exists: function (p) { return call('exists', { path: p }); },
    trash: function (paths) { return call('trash', { paths: paths }); },
    reveal: function (p) { return call('reveal', { path: p }); },
    openPath: function (p) { return call('openPath', { path: p }); },
    openURL: function (u) { return call('openURL', { url: u }); },
    chooseFile: function (o) { return call('chooseFile', o); },
    chooseFolder: function (o) { return call('chooseFolder', o); },
    saveFile: function (o) { return call('saveFile', o); },
    alert: function (o) { return call('alert', o); },
    journal: function (m) { return call('journal', { message: m }); },
    quit: function () { return call('quit'); },

    // ------------------------------------------------- tâches longues
    // SG.task(script, { onOutput(chunk), interval }) -> Promise<exitCode>
    // avec .kill() dessus pour le bouton « Stop ! ».
    task: function (script, opts) {
      opts = opts || {};
      var killed = false, pid = null;
      var p = new Promise(function (resolve) {
        call('taskStart', { script: script }).then(function (t) {
          pid = t.pid;
          var offset = 0;
          var timer = setInterval(function () {
            call('taskPoll', { log: t.log, done: t.done, offset: offset })
              .then(function (r) {
                if (r.chunk && opts.onOutput) opts.onOutput(r.chunk);
                offset = r.offset;
                if (r.exit !== null || killed) {
                  clearInterval(timer);
                  resolve(killed ? -1 : r.exit);
                }
              });
          }, opts.interval || 400);
        });
      });
      p.kill = function () {
        killed = true;
        if (pid !== null) call('taskKill', { pid: pid });
      };
      return p;
    },

    // ------------------------------------------------- utilitaires
    // « video » doit trouver « vidéo » : comparaison sans accents.
    sansAccents: function (s) {
      return (s || '').normalize('NFD').replace(/[̀-ͯ]/g, '');
    },
    correspond: function (texte, filtre) {
      if (!texte) return false;
      return SG.sansAccents(texte).toLowerCase()
        .indexOf(SG.sansAccents(filtre).toLowerCase()) >= 0;
    },
    // tailles à la française, base 1024 (Go / Mo / Ko / octets)
    formatTaille: function (octets) {
      if (octets >= 1073741824) return (octets / 1073741824).toFixed(1).replace('.', ',') + ' Go';
      if (octets >= 1048576) return (octets / 1048576).toFixed(1).replace('.', ',') + ' Mo';
      if (octets >= 1024) return Math.round(octets / 1024) + ' Ko';
      return octets + ' octet' + (octets > 1 ? 's' : '');
    },
    basename: function (p) { return String(p).split('/').pop(); },
    dirname: function (p) {
      var parts = String(p).split('/'); parts.pop();
      return parts.join('/') || '/';
    },
    echap: function (s) {
      return String(s == null ? '' : s)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
    }
  };
  return SG;
})();
