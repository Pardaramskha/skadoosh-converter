// main.js — Skadoosh converter pour macOS (host.js est concaténé avant :
// l'objet Stargazer existe déjà). Toute la logique de conversion vit côté
// page (ui/index.html) ; ici, la fenêtre et une commande maison.

Stargazer.logName = 'skadoosh-converter';

// Écrit des octets (base64) dans un fichier : les écrivains ICO et PDF
// maison vivent côté page (canvas), le natif ne fait que poser les octets.
Stargazer.commands['app.ecrireBase64'] = function (args) {
  var data = $.NSData.alloc.initWithBase64EncodedStringOptions(
    $(String(args.base64)), 0);
  if (data.isNil()) return { err: 'données base64 invalides' };
  var ok = data.writeToFileAtomically($(String(args.path)), true);
  if (!ok) return { err: 'écriture impossible : ' + args.path };
  return { ok: true };
};

Stargazer.createWindow({
  title: 'Skadoosh converter', page: 'index.html',
  width: 600, height: 470,
  resizable: false
});
