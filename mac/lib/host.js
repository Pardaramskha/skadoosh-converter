// host.js — la bibliothèque hôte partagée de Stargazer pour macOS.
// Concaténée AVANT le main.js de chaque app par scripts/build-app.sh, puis
// compilée en applet « stay-open » avec l'osacompile livré avec macOS :
// aucun SDK, aucun runtime à installer — l'équivalent mac du csc.exe intégré.
//
// Chaque app est une fenêtre Cocoa qui héberge son interface en HTML/CSS
// dans une WKWebView (WebKit est livré avec macOS). La page parle au natif
// via window.webkit.messageHandlers.stargazer (voir app-shell.js côté page).

ObjC.import('Cocoa');
ObjC.import('WebKit');

var Stargazer = (function () {
  var S = {};

  // ------------------------------------------------------------- chemins

  // L'applet vit dans son dossier d'app (mac/apps/<id>/ ou mac/ pour le
  // hub). La racine mac/ se reconnaît à son fichier « .stargazer-racine ».
  var fm = $.NSFileManager.defaultManager;

  function parent(p) { return $(p).stringByDeletingLastPathComponent.js; }

  S.bundlePath = $.NSBundle.mainBundle.bundlePath.js;
  S.appDir = parent(S.bundlePath);
  // La racine mac/ du hub (dossier marqué « .stargazer-racine »), ou null
  // quand l'app est installée seule, hors de Stargazer.
  S.hubRoot = (function () {
    var d = S.appDir;
    for (var i = 0; i < 8; i++) {
      if (fm.fileExistsAtPath($(d + '/.stargazer-racine'))) return d;
      var up = parent(d);
      if (up === d) break;
      d = up;
    }
    return null;   // l'app est seule dans la nature
  })();
  S.macRoot = S.hubRoot || S.appDir;
  // Dossier des dépendances, PARTAGÉ par toutes les apps de la famille
  // Stargazer (un moteur n'est jamais téléchargé deux fois) : celui du hub
  // quand l'app y vit, sinon ~/Library/Application Support/Stargazer/dependencies.
  S.depsDir = S.hubRoot ? S.hubRoot + '/dependencies'
    : $.NSHomeDirectory().js + '/Library/Application Support/Stargazer/dependencies';
  S.repoRoot = parent(S.macRoot);

  S.readText = function (path) {
    var s = $.NSString.stringWithContentsOfFileEncodingError(
      $(path), $.NSUTF8StringEncoding, null);
    return s.isNil() ? null : s.js;
  };
  S.writeText = function (path, text) {
    fm.createDirectoryAtPathWithIntermediateDirectoriesAttributesError(
      $(parent(path)), true, $(), null);
    return $(text).writeToFileAtomicallyEncodingError(
      $(path), true, $.NSUTF8StringEncoding, null);
  };
  S.exists = function (path) { return fm.fileExistsAtPath($(path)); };

  // ------------------------------------------------------------- journal
  // Comme sur Windows : l'échec silencieux est le risque n°1 d'une famille
  // d'apps sans terminal. Tout pépin va dans logs/<app>.log.

  S.logName = 'app';
  S.journal = function (message) {
    try {
      var d = $.NSDateFormatter.alloc.init;
      d.dateFormat = 'yyyy-MM-dd HH:mm:ss';
      var line = d.stringFromDate($.NSDate.date).js + '  ' + message + '\n';
      var path = S.appDir + '/logs/' + S.logName + '.log';
      var old = S.readText(path);
      S.writeText(path, (old || '') + line);
    } catch (e) { /* le journal ne doit jamais faire échouer l'appli */ }
  };

  // ------------------------------------------------------------- shell

  var osa = Application.currentApplication();
  osa.includeStandardAdditions = true;

  S.q = function (s) { return "'" + String(s).replace(/'/g, "'\\''") + "'"; };

  // Exécution synchrone (petites commandes). Retourne {ok, out|err, code}.
  S.shell = function (script) {
    try { return { ok: true, out: osa.doShellScript(script) }; }
    catch (e) {
      return { ok: false, err: String(e.message || e),
               code: (e.errorNumber !== undefined ? e.errorNumber : 1) };
    }
  };

  // Tâche longue (ffmpeg, yt-dlp…) : le script tourne détaché, sa sortie
  // va dans un fichier journal que la page vient lire par petits bouts
  // (taskPoll) — pas de rappel inter-threads, donc pas de surprises JXA.
  var taskSeq = 0;
  S.taskStart = function (script) {
    taskSeq++;
    var dir = $.NSTemporaryDirectory().js + 'stargazer-task-' +
      $.NSProcessInfo.processInfo.processIdentifier + '-' + taskSeq;
    fm.createDirectoryAtPathWithIntermediateDirectoriesAttributesError(
      $(dir), true, $(), null);
    var runner = dir + '/run.zsh', log = dir + '/log.txt',
        done = dir + '/done.txt';
    // sous-shell : même si le script fait « exit », le code de sortie est
    // toujours écrit dans done.txt (sinon la page attendrait pour rien)
    S.writeText(runner,
      '#!/bin/zsh\n(\n' + script + '\n)\nprint -r -- $? > ' + S.q(done) + '\n');
    var r = S.shell('nohup /bin/zsh ' + S.q(runner) + ' > ' + S.q(log) +
      ' 2>&1 & disown; echo $!');
    return { id: taskSeq, pid: r.ok ? parseInt(r.out, 10) : -1,
             log: log, done: done };
  };
  S.taskPoll = function (args) {
    var content = S.readText(args.log) || '';
    var chunk = content.length > args.offset ? content.slice(args.offset) : '';
    var exit = null;
    var d = S.readText(args.done);
    if (d !== null) exit = parseInt(d.trim(), 10);
    return { chunk: chunk, offset: content.length, exit: exit };
  };
  S.taskKill = function (pid) {
    // le runner zsh a des enfants : on tue tout le groupe
    S.shell('pkill -TERM -P ' + parseInt(pid, 10) + ' ; kill -TERM ' +
      parseInt(pid, 10) + ' 2>/dev/null ; true');
    return { ok: true };
  };

  // ------------------------------------------------------------- fichiers

  S.trash = function (paths) {
    var n = 0, echecs = [];
    paths.forEach(function (p) {
      var url = $.NSURL.fileURLWithPath($(p));
      var ok = fm.trashItemAtURLResultingItemURLError(url, null, null);
      if (ok) n++; else echecs.push(p);
    });
    return { n: n, echecs: echecs };
  };
  S.reveal = function (path) {
    $.NSWorkspace.sharedWorkspace
      .selectFileInFileViewerRootedAtPath($(path), $(''));
    return { ok: true };
  };
  S.openPath = function (path) {
    $.NSWorkspace.sharedWorkspace.openFile($(path));
    return { ok: true };
  };
  S.openURL = function (url) {
    $.NSWorkspace.sharedWorkspace.openURL($.NSURL.URLWithString($(url)));
    return { ok: true };
  };

  // ------------------------------------------------------------- dialogues

  S.chooseFile = function (args) {
    args = args || {};
    var p = $.NSOpenPanel.openPanel;
    p.canChooseFiles = true; p.canChooseDirectories = false;
    p.allowsMultipleSelection = !!args.multiple;
    if (args.prompt) p.message = $(args.prompt);
    if (args.dir) p.directoryURL = $.NSURL.fileURLWithPath($(args.dir));
    if (args.types && args.types.length)
      p.allowedFileTypes = $(args.types);   // extensions sans point
    if (p.runModal !== $.NSModalResponseOK) return { paths: null };
    // deepUnwrap ne déballe PAS les NSURL (objets, pas de .replace) ;
    // .path donne le chemin POSIX déjà décodé (pas de %20 à traîner)
    var paths = [];
    for (var i = 0; i < p.URLs.count; i++)
      paths.push(p.URLs.objectAtIndex(i).path.js);
    return { paths: paths };
  };
  S.chooseFolder = function (args) {
    args = args || {};
    var p = $.NSOpenPanel.openPanel;
    p.canChooseFiles = false; p.canChooseDirectories = true;
    p.canCreateDirectories = true;
    if (args.prompt) p.message = $(args.prompt);
    if (args.dir) p.directoryURL = $.NSURL.fileURLWithPath($(args.dir));
    if (p.runModal !== $.NSModalResponseOK) return { path: null };
    return { path: p.URLs.objectAtIndex(0).path.js };
  };
  S.saveFile = function (args) {
    args = args || {};
    var p = $.NSSavePanel.savePanel;
    if (args.name) p.nameFieldStringValue = $(args.name);
    if (args.prompt) p.message = $(args.prompt);
    if (args.dir) p.directoryURL = $.NSURL.fileURLWithPath($(args.dir));
    if (p.runModal !== $.NSModalResponseOK) return { path: null };
    return { path: p.URL.path.js };
  };
  S.alert = function (args) {
    var a = $.NSAlert.alloc.init;
    a.messageText = $(args.title || 'Stargazer');
    a.informativeText = $(args.message || '');
    (args.buttons || ['OK']).forEach(function (b) { a.addButtonWithTitle($(b)); });
    var r = a.runModal;   // 1000 = premier bouton, 1001 = deuxième…
    return { button: r - 1000 };
  };

  // ------------------------------------------------------------- fenêtres

  var bridgeClassMade = false, dropClassMade = false, delegateMade = false;
  var windows = [];   // { win, webview, page } — page = objet de config

  function jsInto(webview, code) {
    webview.evaluateJavaScriptCompletionHandler(code, function (r, e) {});
  }

  // Dispatch d'une commande du pont. Les apps enregistrent leurs commandes
  // maison via Stargazer.commands.maCommande = function(args) {...}.
  S.commands = {};
  function dispatch(webview, id, cmd, args) {
    var result;
    try {
      switch (cmd) {
        case 'env': result = {
          appDir: S.appDir, macRoot: S.macRoot, depsDir: S.depsDir,
          repoRoot: S.repoRoot, home: $.NSHomeDirectory().js,
          sep: '/', platform: 'mac'
        }; break;
        case 'shell':       result = S.shell(args.script); break;
        case 'taskStart':   result = S.taskStart(args.script); break;
        case 'taskPoll':    result = S.taskPoll(args); break;
        case 'taskKill':    result = S.taskKill(args.pid); break;
        case 'read':        result = { text: S.readText(args.path) }; break;
        case 'write':       result = { ok: S.writeText(args.path, args.text) }; break;
        case 'exists':      result = { exists: S.exists(args.path) }; break;
        case 'trash':       result = S.trash(args.paths); break;
        case 'reveal':      result = S.reveal(args.path); break;
        case 'openPath':    result = S.openPath(args.path); break;
        case 'openURL':     result = S.openURL(args.url); break;
        case 'chooseFile':  result = S.chooseFile(args); break;
        case 'chooseFolder': result = S.chooseFolder(args); break;
        case 'saveFile':    result = S.saveFile(args); break;
        case 'alert':       result = S.alert(args); break;
        case 'journal':     S.journal(args.message); result = { ok: true }; break;
        case 'setTitle':
          if (windows.length) windows[0].win.title = $(String(args.title));
          result = { ok: true }; break;
        case 'quit':
          $.NSApplication.sharedApplication.terminate(null);
          result = { ok: true }; break;
        default:
          if (S.commands[cmd]) result = S.commands[cmd](args);
          else result = { err: 'commande inconnue : ' + cmd };
      }
    } catch (e) {
      result = { err: String(e.message || e) };
      S.journal('Commande « ' + cmd + ' » : ' + result.err);
    }
    jsInto(webview,
      'window.SG && SG.__resolve(' + JSON.stringify(id) + ',' +
      JSON.stringify(JSON.stringify(result === undefined ? null : result)) + ')');
  }

  function makeBridgeClass() {
    if (bridgeClassMade) return; bridgeClassMade = true;
    ObjC.registerSubclass({
      name: 'SGBridge',
      protocols: ['WKScriptMessageHandler'],
      methods: {
        'userContentController:didReceiveScriptMessage:': {
          types: ['void', ['id', 'id']],
          implementation: function (ucc, message) {
            var body;
            try { body = JSON.parse(ObjC.unwrap(message.body)); }
            catch (e) { return; }
            var wv = null;
            for (var i = 0; i < windows.length; i++)
              if (message.webView && windows[i].webview.isEqual(message.webView))
                wv = windows[i].webview;
            if (!wv) wv = windows.length ? windows[windows.length - 1].webview : null;
            if (wv) dispatch(wv, body.id, body.cmd, body.args || {});
          }
        }
      }
    });
  }

  // Dépôt de fichiers : la WKWebView moderne cache les chemins au HTML5,
  // alors on intercepte le drag natif et on pousse les chemins à la page.
  function makeDropClass() {
    if (dropClassMade) return; dropClassMade = true;
    ObjC.registerSubclass({
      name: 'SGWebView',
      superclass: 'WKWebView',
      methods: {
        // Panneau non-activant (menu radial) : l'app ne s'active jamais,
        // donc CHAQUE clic est un « premier clic » — sans ce oui, la vue
        // les avale tous au lieu de les livrer à la page.
        'acceptsFirstMouse:': {
          types: ['B', ['id']],
          implementation: function (ev) { return true; }
        },
        'draggingEntered:': {
          types: ['Q', ['id']],
          implementation: function (info) { return $.NSDragOperationCopy; }
        },
        'draggingUpdated:': {
          types: ['Q', ['id']],
          implementation: function (info) { return $.NSDragOperationCopy; }
        },
        'performDragOperation:': {
          types: ['B', ['id']],
          implementation: function (info) {
            try {
              var pb = info.draggingPasteboard;
              var urls = pb.readObjectsForClassesOptions(
                $([$.NSURL.class]), $({ NSPasteboardURLReadingFileURLsOnlyKey: true }));
              if (!urls.isNil() && urls.count > 0) {
                var paths = [];
                for (var i = 0; i < urls.count; i++)
                  paths.push(urls.objectAtIndex(i).path.js);
                for (var w = 0; w < windows.length; w++)
                  jsInto(windows[w].webview,
                    'window.SG && SG.__drop(' +
                    JSON.stringify(JSON.stringify(paths)) + ')');
                return true;
              }
            } catch (e) { }
            return false;
          }
        }
      }
    });
  }

  var keyWinMade = false;
  function makeKeyWindowClass() {
    // Une fenêtre sans cadre (menu radial) ne peut pas devenir « key » par
    // défaut : Échap et les chiffres 1-6 ne répondraient pas. Et surtout,
    // c'est un NSPanel « non-activating » (l'astuce de Spotlight) : il
    // s'affiche ET prend le clavier par-dessus n'importe quelle app, sans
    // que macOS n'ait à activer la nôtre — indispensable depuis que les
    // apps en arrière-plan n'ont plus le droit de voler l'activation.
    if (keyWinMade) return; keyWinMade = true;
    ObjC.registerSubclass({
      name: 'SGKeyPanel',
      superclass: 'NSPanel',
      methods: {
        'canBecomeKeyWindow': {
          types: ['B', []],
          implementation: function () { return true; }
        }
      }
    });
  }

  function makeDelegateClass() {
    if (delegateMade) return; delegateMade = true;
    ObjC.registerSubclass({
      name: 'SGWinDelegate',
      protocols: ['NSWindowDelegate'],
      methods: {
        'windowWillClose:': {
          types: ['void', ['id']],
          implementation: function (note) {
            // comportement par défaut d'une app : fermer = quitter ;
            // les apps « de fond » (hub, Macro Polo) posent plutôt un
            // rappel — p. ex. pour quitter le Dock en se rangeant
            if (S.quitWhenClosed)
              $.NSApplication.sharedApplication.terminate(null);
            else if (S.onWindowClosed)
              try { S.onWindowClosed(); } catch (e) { }
          }
        }
      }
    });
  }

  S.quitWhenClosed = true;
  S.onWindowClosed = null;   // rappel optionnel quand quitWhenClosed=false

  // Barre de menus standard : sans elle, ⌘C/⌘V/⌘Q ne répondent pas dans
  // la WKWebView. (Le rendu WinForms n'avait pas ce souci ; Cocoa, si.)
  function makeMainMenu(appName) {
    var mainMenu = $.NSMenu.alloc.init;

    var appItem = $.NSMenuItem.alloc.init;
    mainMenu.addItem(appItem);
    var appMenu = $.NSMenu.alloc.init;
    appMenu.addItem($.NSMenuItem.alloc.initWithTitleActionKeyEquivalent(
      $('Masquer ' + appName), 'hide:', $('h')));
    appMenu.addItem($.NSMenuItem.separatorItem);
    appMenu.addItem($.NSMenuItem.alloc.initWithTitleActionKeyEquivalent(
      $('Quitter ' + appName), 'terminate:', $('q')));
    appItem.submenu = appMenu;

    var editItem = $.NSMenuItem.alloc.init;
    mainMenu.addItem(editItem);
    var edit = $.NSMenu.alloc.initWithTitle($('Édition'));
    [['Annuler', 'undo:', 'z'], ['Rétablir', 'redo:', 'Z'],
     ['-'], ['Couper', 'cut:', 'x'], ['Copier', 'copy:', 'c'],
     ['Coller', 'paste:', 'v'], ['Tout sélectionner', 'selectAll:', 'a']
    ].forEach(function (it) {
      if (it[0] === '-') { edit.addItem($.NSMenuItem.separatorItem); return; }
      edit.addItem($.NSMenuItem.alloc.initWithTitleActionKeyEquivalent(
        $(it[0]), it[1], $(it[2])));
    });
    editItem.submenu = edit;

    var winItem = $.NSMenuItem.alloc.init;
    mainMenu.addItem(winItem);
    var winMenu = $.NSMenu.alloc.initWithTitle($('Fenêtre'));
    winMenu.addItem($.NSMenuItem.alloc.initWithTitleActionKeyEquivalent(
      $('Réduire'), 'performMiniaturize:', $('m')));
    winMenu.addItem($.NSMenuItem.alloc.initWithTitleActionKeyEquivalent(
      $('Fermer'), 'performClose:', $('w')));
    winItem.submenu = winMenu;

    // Aide : les actions sont poussées à la page (SG.on('verifierMaj') et
    // SG.on('aPropos')) — le standard « Vérifier les mises à jour » de la
    // famille Stargazer.
    makeMenuTargetClass();
    menuTarget = $.SGMenuTarget.alloc.init;
    var aideItem = $.NSMenuItem.alloc.init;
    mainMenu.addItem(aideItem);
    var aide = $.NSMenu.alloc.initWithTitle($('Aide'));
    var verif = $.NSMenuItem.alloc.initWithTitleActionKeyEquivalent(
      $('Vérifier les mises à jour…'), 'verifierMaj:', $(''));
    verif.target = menuTarget;
    aide.addItem(verif);
    aide.addItem($.NSMenuItem.separatorItem);
    var apropos = $.NSMenuItem.alloc.initWithTitleActionKeyEquivalent(
      $('À propos de ' + appName), 'aPropos:', $(''));
    apropos.target = menuTarget;
    aide.addItem(apropos);
    aideItem.submenu = aide;

    $.NSApplication.sharedApplication.mainMenu = mainMenu;
  }

  var menuTarget = null;
  var menuTargetClassMade = false;
  function makeMenuTargetClass() {
    if (menuTargetClassMade) return; menuTargetClassMade = true;
    function pousser(nom) {
      var mw = S.mainWindow();
      if (mw) S.pushToPage(mw.webview, "__push.bind(null,'" + nom + "')", {});
    }
    ObjC.registerSubclass({
      name: 'SGMenuTarget',
      methods: {
        'verifierMaj:': { types: ['void', ['id']], implementation: function (s) { pousser('verifierMaj'); } },
        'aPropos:': { types: ['void', ['id']], implementation: function (s) { pousser('aPropos'); } }
      }
    });
  }

  // Crée la fenêtre principale d'une app et charge son ui/<page>.
  // opts : { title, page, width, height, minWidth, minHeight,
  //          resizable (défaut true), frame (borderless rond, pour le
  //          menu radial), transparent, level, x, y, quitOnClose }
  S.createWindow = function (opts) {
    makeBridgeClass(); makeDropClass(); makeDelegateClass();
    makeKeyWindowClass();
    if (opts.quitOnClose !== undefined) S.quitWhenClosed = opts.quitOnClose;

    var ucc = $.WKUserContentController.alloc.init;
    ucc.addScriptMessageHandlerName($.SGBridge.alloc.init, 'stargazer');
    var cfg = $.WKWebViewConfiguration.alloc.init;
    cfg.userContentController = ucc;
    try {   // autoriser file:// -> file:// (pages + ressources locales)
      cfg.preferences.setValueForKey(true, 'allowFileAccessFromFileURLs');
      cfg.setValueForKey(true, '_allowUniversalAccessFromFileURLs');
    } catch (e) { }

    var w = opts.width || 760, h = opts.height || 566;
    var style = opts.frame === false
      ? $.NSWindowStyleMaskBorderless | $.NSWindowStyleMaskNonactivatingPanel
      : $.NSWindowStyleMaskTitled | $.NSWindowStyleMaskClosable |
        $.NSWindowStyleMaskMiniaturizable |
        (opts.resizable === false ? 0 : $.NSWindowStyleMaskResizable);

    var Klass = opts.frame === false ? $.SGKeyPanel : $.NSWindow;
    var win = Klass.alloc.initWithContentRectStyleMaskBackingDefer(
      $.NSMakeRect(opts.x || 0, opts.y || 0, w, h), style,
      $.NSBackingStoreBuffered, false);
    if (opts.frame === false) {
      // le panneau reste visible même si notre app n'est pas active
      win.hidesOnDeactivate = false;
      win.becomesKeyOnlyIfNeeded = false;
    }
    win.title = $(opts.title || 'Stargazer');
    win.releasedWhenClosed = false;
    // référence FORTE gardée plus bas : NSWindow.delegate est faible, un
    // délégué anonyme se fait ramasser et windowWillClose ne part jamais
    // (l'app resterait zombie dans le Dock après la croix rouge)
    var delegue = $.SGWinDelegate.alloc.init;
    win.delegate = delegue;
    if (opts.minWidth)
      win.minSize = $.NSMakeSize(opts.minWidth, opts.minHeight || 400);
    if (opts.transparent) {
      win.opaque = false;
      // alpha à 1 % et pas zéro : une fenêtre au fond parfaitement
      // transparent peut devenir « cliquable au travers » — le serveur
      // de fenêtres laisserait passer les clics vers l'app du dessous
      win.backgroundColor =
        $.NSColor.colorWithCalibratedWhiteAlpha(0, 0.01);
      win.hasShadow = false;
    }
    if (opts.level === 'top') win.level = $.NSPopUpMenuWindowLevel;

    var webview = $.SGWebView.alloc.initWithFrameConfiguration(
      $.NSMakeRect(0, 0, w, h), cfg);
    webview.autoresizingMask = $.NSViewWidthSizable | $.NSViewHeightSizable;
    if (opts.transparent) {
      try { webview.setValueForKey(false, 'drawsBackground'); } catch (e) { }
    }
    var pageURL = $.NSURL.fileURLWithPath($(S.appDir + '/ui/' + opts.page));
    // readAccess élargit la lecture file:// au-delà d'ui/ (ex. « / »
    // pour qu'un aperçu affiche des images locales quelconques)
    var dirURL = $.NSURL.fileURLWithPath($(opts.readAccess || S.appDir + '/ui'));
    webview.loadFileURLAllowingReadAccessToURL(pageURL, dirURL);
    win.contentView = webview;
    if (opts.x === undefined) win.center;
    if (opts.frame === false) {
      // par-dessus l'app active, sans activer la nôtre ni voler le focus
      win.orderFrontRegardless;
      win.makeKeyAndOrderFront(null);
    } else {
      win.makeKeyAndOrderFront(null);
      $.NSApplication.sharedApplication.activateIgnoringOtherApps(true);
    }

    windows.push({ win: win, webview: webview, delegate: delegue });
    makeMainMenu(opts.title || 'Stargazer');
    return { win: win, webview: webview };
  };

  S.mainWindow = function () { return windows.length ? windows[0] : null; };
  // à appeler quand une fenêtre éphémère (menu radial) se ferme : sinon
  // le repli du pont (« dernière fenêtre ») pourrait viser un fantôme
  S.forgetWindow = function (win) {
    for (var i = windows.length - 1; i >= 0; i--) {
      try { if (windows[i].win.isEqual(win)) windows.splice(i, 1); }
      catch (e) { }
    }
  };
  S.pushToPage = function (webview, fn, payload) {
    jsInto(webview, 'window.SG && SG.' + fn + '(' +
      JSON.stringify(JSON.stringify(payload)) + ')');
  };

  return S;
})();

// L'applet est compilé « stay-open » : idle est appelé périodiquement,
// on n'a rien à y faire — la vie se passe dans les rappels Cocoa.
function idle() { return 86400; }
