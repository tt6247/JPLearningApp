/* 假名小步 · 安卓版 —— 界面（页面结构与桌面版 MainForm 对应） */
(function () {
  'use strict';
  var C = window.KanaCore, Plan = C.DailyPlan, Timer = C.StudyTimer;
  var bridge = window.AndroidBridge || null;
  var STORE_KEY = 'kana-small-steps-android-v1';
  var AUTO_SAVE_SECONDS = 30;
  var RING = 2 * Math.PI * 84;

  // ---------- 存储：安卓用原生桥写入应用私有目录；浏览器调试时退回 localStorage ----------
  function loadRaw() {
    try {
      var s = bridge ? bridge.loadProgress() : localStorage.getItem(STORE_KEY);
      return s ? JSON.parse(s) : null;
    } catch (e) { return null; }
  }
  var W = C.Words;
  W.load(window.KANA_WORDS || []); // 先载入词表，读取进度时才能校验已记住的单词
  var progress = C.Progress.fromJSON(loadRaw());
  var timer = Timer.restore(progress.timerState, progress.timerMinutes, Date.now());
  var unsaved = 0, saveWarned = false;

  function save() {
    progress.timerState = timer.snapshot();
    var json = JSON.stringify(progress.toJSON()), ok = true;
    try {
      if (bridge) ok = bridge.saveProgress(json);
      else localStorage.setItem(STORE_KEY, json);
    } catch (e) { ok = false; }
    if (!ok && !saveWarned) { saveWarned = true; toast('暂时无法保存进度，本次仍可继续学习。'); }
    if (ok) saveWarned = false;
  }

  // ---------- 外观（亮色护眼 / 深色护眼 / 跟随系统） ----------
  var THEME_INFO = {
    system: { name: '跟随系统', desc: '白天亮色、夜间深色，随手机设置自动切换', swatch: ['#F2EDE1', '#1D1C1A'] },
    light: { name: '亮色护眼', desc: '暖色纸张底，柔和不刺眼，适合白天和明亮环境', swatch: ['#F2EDE1', '#5F57A8'] },
    dark: { name: '深色护眼', desc: '暖灰黑底、米白文字，夜间和弱光下更舒适', swatch: ['#1D1C1A', '#A99DDC'] }
  };
  var systemDark = false;
  function readSystemDark() {
    var v = call('systemDark');
    if (typeof v === 'boolean') return v;
    return !!(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
  }
  function isDark() { return progress.theme === 'dark' || (progress.theme === 'system' && systemDark); }
  function applyTheme(animate) {
    var dark = isDark();
    if (animate) {
      document.body.classList.add('theming');
      setTimeout(function () { document.body.classList.remove('theming'); }, 350);
    }
    if (dark) document.documentElement.setAttribute('data-theme', 'dark');
    else document.documentElement.removeAttribute('data-theme');
    call('setDarkTheme', dark); // 同步状态栏、导航栏颜色
    call('rememberTheme', progress.theme);
  }
  function setTheme(mode) {
    progress.theme = mode;
    save();
    applyTheme(true);
    openThemeSheet();
  }
  function openThemeSheet() {
    openSheet('<h2 style="margin:4px 0 4px">外观</h2><p class="hint" style="margin:0 0 12px">两套配色都使用暖色、低刺激的护眼色调</p><div class="themes">' +
      C.THEMES.map(function (m) {
        var t = THEME_INFO[m], on = progress.theme === m;
        return '<button class="theme-opt' + (on ? ' active' : '') + '" data-theme-mode="' + m + '" aria-pressed="' + on + '">' +
          '<span class="swatch"><i style="background:' + t.swatch[0] + '"></i><i style="background:' + t.swatch[1] + '"></i></span>' +
          '<span><b>' + t.name + '</b><small>' + t.desc + '</small></span><span class="tick">' + (on ? '✓' : '') + '</span></button>';
      }).join('') + '</div><button class="btn block" style="margin-top:12px" data-act="close-sheet">完成</button>');
  }

  // ---------- 状态 ----------
  var page = 'today', category = 'basic', tableMode = 'table', scope = 'basic', quizMode = 'read';
  var cardMode = 'kana', wordFilter = 'all', wordIndex = 0, wordFlipped = false; // 记忆卡：假名 / 单词（2.6.0）
  var deck = [], deckShuffled = false, cardIndex = 0, flipped = false, quiz = null, sheetKana = null;
  var view = document.getElementById('view');
  function $(id) { return document.getElementById(id); }

  // ---------- 小工具 ----------
  var toastTimer;
  function toast(t) {
    var el = $('toast');
    el.textContent = t; el.classList.remove('hidden');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { el.classList.add('hidden'); }, 3000);
  }
  function call(name) {
    if (!bridge || typeof bridge[name] !== 'function') return undefined;
    try { return bridge[name].apply(bridge, Array.prototype.slice.call(arguments, 1)); } catch (e) { return undefined; }
  }
  function vibrate(ms) { if (call('vibrate', ms) === undefined && navigator.vibrate) navigator.vibrate(ms); }

  var player = new Audio();
  function speak(x) {
    try {
      player.pause();
      player.src = 'audio/' + C.audioKey(x) + '.mp3';
      var p = player.play();
      if (p && p.catch) p.catch(function () { toast('暂时无法播放发音，请检查媒体音量。'); });
    } catch (e) { toast('暂时无法播放发音。'); }
  }

  var actx = null;
  function chime() {
    try {
      actx = actx || new (window.AudioContext || window.webkitAudioContext)();
      if (actx.state === 'suspended') actx.resume();
      [660, 880, 990].forEach(function (f, i) {
        var o = actx.createOscillator(), g = actx.createGain(), at = actx.currentTime + i * 0.28;
        o.frequency.value = f;
        g.gain.setValueAtTime(0.0001, at);
        g.gain.exponentialRampToValueAtTime(0.3, at + 0.02);
        g.gain.exponentialRampToValueAtTime(0.0001, at + 0.7);
        o.connect(g); g.connect(actx.destination); o.start(at); o.stop(at + 0.75);
      });
    } catch (e) { /* 无音频也不影响 */ }
  }

  function heading(title, sub) { return '<h1>' + title + '</h1><p class="sub">' + sub + '</p>'; }
  function kanaSpan(k) { return '<span lang="ja">' + k + '</span>'; }
  function pct(a, b) { return b ? Math.round(100 * a / b) : 0; }

  // ---------- 导航 ----------
  function navigate(p) {
    if (p === 'cards' && page !== 'cards') resetDeck();
    if (p === 'quiz') quiz = newQuiz(scope);
    page = p;
    render();
    window.scrollTo(0, 0);
  }

  function render() {
    if (Strokes) Strokes.stop();
    var tabs = document.querySelectorAll('#tabbar button');
    for (var i = 0; i < tabs.length; i++) tabs[i].classList.toggle('active', tabs[i].dataset.page === page);
    if (page === 'today') renderToday();
    else if (page === 'table') renderTable();
    else if (page === 'cards') renderCards();
    else if (page === 'quiz') renderQuiz();
    else if (page === 'errors') renderErrors();
    else renderHelp();
    updateTimerUI();
  }

  // ---------- 今日计划 ----------
  function renderToday() {
    var now = progress.clock(), t = progress.today(), tasks = Plan.build(progress);
    var done = tasks.filter(function (x) { return x.done; }).length;
    var week = Plan.week(progress), top = progress.goal;
    week.forEach(function (w) { top = Math.max(top, w.minutes); });
    var html = heading(Plan.greeting(now) + '，今天也学一小步。', Plan.coach(progress));
    html += '<div class="stats">' +
      stat('连续学习', progress.streak(), ' 天') +
      '<div class="stat"><small>今日专注</small><strong id="today-min"></strong></div>' +
      stat('今日答题', t.answered, ' 题' + (t.answered ? ' · ' + pct(t.correct, t.answered) + '%' : '')) + '</div>';

    html += '<section class="card"><div class="row"><h2>今日学习建议</h2><span class="hint">' + done + ' / ' + tasks.length + ' 完成</span></div>';
    tasks.forEach(function (x, i) {
      html += '<div class="task' + (x.done ? ' done' : '') + '"><span class="check" aria-label="' + (x.done ? '已完成' : '未完成') + '">' + (x.done ? '✓' : '') + '</span>' +
        '<div class="task-body"><h3>' + x.title + '</h3><p>' + (x.kana ? kanaSpan(x.kana) + ' · ' : '') + x.detail + '</p></div>' +
        (x.action ? '<button class="btn ' + (x.done ? 'outline' : 'primary') + '" data-task="' + i + '">' + (x.done ? x.doneButton : x.button) + '</button>' : '') + '</div>';
    });
    html += '</section>';
    window.__tasks = tasks;

    html += '<section class="card timer"><div class="row"><h2>专注计时</h2><span class="hint" id="t-state"></span></div>' +
      '<div class="ring"><svg viewBox="0 0 190 190" width="190" height="190" aria-hidden="true"><circle class="track" cx="95" cy="95" r="84"/>' +
      '<circle class="prog" id="t-ring" cx="95" cy="95" r="84" stroke-dasharray="' + RING.toFixed(1) + '" stroke-dashoffset="0"/></svg>' +
      '<div class="ring-text"><strong id="t-time">00:00</strong><span id="t-total"></span></div></div>' +
      '<div class="bar"><i id="t-bar" style="width:0"></i></div>' +
      '<div class="presets">' + C.TIMER_CHOICES.map(function (m) { return '<button data-preset="' + m + '">' + m + ' 分钟</button>'; }).join('') + '</div>' +
      '<div class="btns even"><button class="btn primary" id="t-toggle" data-act="timer-toggle">开始专注</button><button class="btn" data-act="timer-add">+5 分钟</button><button class="btn" data-act="timer-reset">重置</button></div>' +
      '<div class="goal"><span>每日目标</span><button class="picker-trigger" data-act="open-goal" aria-haspopup="dialog" aria-label="每日目标：' + progress.goal + ' 分钟"><span>' + progress.goal + ' 分钟</span></button></div><div class="bar green"><i id="goal-fill" style="width:0"></i></div>' +
      '<p class="hint" style="margin-top:10px">时长按计时器统计。切换页面计时不停；离开应用时，到点会发通知提醒。</p></section>';

    html += '<section class="card"><div class="row" style="margin-bottom:8px"><h2>近 7 天专注时长</h2></div><div class="week">' +
      '<div class="goal-line" style="bottom:' + (24 + 100 * progress.goal / top) + 'px"><span>目标 ' + progress.goal + ' 分</span></div>' +
      week.map(function (w) {
        var h = w.minutes ? Math.max(6, 100 * w.minutes / top) : (w.studied ? 6 : 3);
        var cls = w.minutes >= progress.goal ? 'met' : (w.minutes || w.studied ? 'part' : '');
        return '<div class="col' + (w.isToday ? ' today' : '') + '"><span>' + (w.minutes ? w.minutes + '分' : (w.studied ? '✓' : '')) + '</span><i class="' + cls + '" style="height:' + h + 'px"></i>' + w.label + '</div>';
      }).join('') + '</div></section>';
    html += '<div class="notice">小贴士：' + Plan.tipOfDay(now) + '</div>';
    view.innerHTML = html;
  }

  function stat(label, value, unit) {
    return '<div class="stat"><small>' + label + '</small><strong>' + value + '<span>' + unit + '</span></strong></div>';
  }

  function runTask(t) {
    if (t.action === 'reviewQuiz' || t.action === 'quiz' || t.action === 'allQuiz') quizMode = 'read';
    if (t.action === 'reviewQuiz') startQuiz('errors');
    else if (t.action === 'warmup') startCards('learned');
    else if (t.action === 'learnGroup') startCards(t.scope);
    else if (t.action === 'quiz') startQuiz(t.scope);
    else if (t.action === 'allQuiz') startQuiz('mix');
    else if (t.action === 'startTimer') startGoalTimer();
  }

  // ---------- 计时器 ----------
  function scheduleAlarm() {
    if (timer.running) {
      var goalMet = (progress.today().sec + timer.remaining) / 60 >= progress.goal;
      call('scheduleTimer', String(Math.round(timer.endAt)), '时间到！', goalMet ? '今日目标已达成，好好休息一下。' : '起身活动 5 分钟，喝口水，再开始下一段。');
    } else {
      call('cancelTimer');
    }
    call('keepScreenOn', timer.running);
  }

  function toggleTimer() {
    var now = Date.now();
    if (timer.running) {
      progress.addStudySeconds(timer.pause(now));
      unsaved = 0;
      toast('已暂停计时，休息一下也没关系。');
    } else {
      timer.start(now);
      call('requestNotificationPermission');
      toast('开始专注 ' + Math.round(timer.remaining / 60) + ' 分钟。切换页面计时不停。');
    }
    scheduleAlarm(); save(); updateTimerUI();
  }
  function resetTimer() {
    if (timer.running) progress.addStudySeconds(timer.pause(Date.now()));
    timer.reset(); unsaved = 0; scheduleAlarm(); save(); updateTimerUI();
  }
  function addFive() { timer.addSeconds(300); scheduleAlarm(); save(); updateTimerUI(); }
  function setPreset(m) { if (timer.set(m)) { progress.timerMinutes = m; save(); } updateTimerUI(); }
  function startGoalTimer() {
    if (timer.running) { toast('计时正在进行中，继续加油！'); return; }
    timer.set(Plan.suggestedTimerMinutes(progress));
    toggleTimer();
  }

  function onTick() {
    var r = timer.tick(Date.now());
    if (r.credit > 0) { progress.addStudySeconds(r.credit); unsaved += r.credit; }
    if (r.finished) { onFinished(); return; }
    if (unsaved >= AUTO_SAVE_SECONDS) { unsaved = 0; save(); }
    updateTimerUI();
  }

  function onFinished() {
    unsaved = 0;
    call('cancelTimer');
    call('keepScreenOn', false);
    save();
    var goalMet = progress.today().sec / 60 >= progress.goal;
    var msg = goalMet ? '今日目标已达成，好好休息一下。' : '起身活动 5 分钟，喝口水，再开始下一段。';
    chime(); vibrate(600);
    if (page === 'today') render(); else updateTimerUI();
    openSheet('<div style="font-size:46px;margin:6px 0">⏰</div><h2>时间到！</h2><p class="sub" style="margin:8px 0 16px">' + msg +
      '</p><button class="btn primary block" data-act="close-sheet">好的</button>');
  }

  function updateTimerUI() {
    var time = Timer.format(timer.remaining), minutes = Math.floor(progress.today().sec / 60);
    var started = timer.started(), fin = timer.finished();
    var stateText = timer.running ? '专注中…' : started ? '已暂停' : fin ? '时间到 ✓' : '准备开始';
    var frac = fin ? 1 : started ? timer.elapsed() : 0;
    var pill = $('pill');
    pill.classList.toggle('hidden', !(started || timer.running) || page === 'today');
    pill.classList.toggle('running', timer.running);
    $('pill-time').textContent = time;
    if ($('t-time')) {
      $('t-time').textContent = time;
      $('t-total').textContent = '本段共 ' + Math.round(timer.total / 60) + ' 分钟';
      $('t-state').textContent = stateText;
      $('t-ring').setAttribute('stroke-dashoffset', (RING * frac).toFixed(1));
      $('t-bar').style.width = (frac * 100) + '%';
      $('t-toggle').textContent = timer.running ? '暂停' : started ? '继续' : fin ? '再来一段' : '开始专注';
      var ps = document.querySelectorAll('[data-preset]');
      for (var i = 0; i < ps.length; i++) {
        ps[i].disabled = timer.running;
        ps[i].classList.toggle('active', Number(ps[i].dataset.preset) * 60 === timer.total);
      }
    }
    if ($('today-min')) $('today-min').innerHTML = minutes + '<span> / ' + progress.goal + ' 分</span>';
    if ($('goal-fill')) $('goal-fill').style.width = Math.min(100, progress.today().sec / 60 / progress.goal * 100) + '%';
  }

  // ---------- 五十音表 / 分组学习 ----------
  // 五十音表的字形切换（2.5.0）：平假名 / 片假名 / 平片对照，选择记在进度里
  function scriptTabs() {
    return '<div class="seg script" role="tablist" aria-label="字形">' + [['hira', '平假名'], ['kata', '片假名'], ['both', '平片对照']].map(function (c) {
      var on = progress.script === c[0];
      return '<button data-script="' + c[0] + '" role="tab" aria-selected="' + on + '" class="' + (on ? 'active' : '') + '">' + c[1] + '</button>';
    }).join('') + '</div>';
  }
  /** 当前字形下某类别的范围；对照模式下以平假名为准。 */
  function catScope(cat) { return C.typeFor(cat, progress.script === 'kata' ? 'kata' : 'hira'); }

  function pairCell(x, combo) {
    var k = C.ByKana[x.pair];
    return '<button class="kcell pair' + (combo ? ' combo' : '') + '" data-kana="' + x.K + '" aria-label="' + x.K + ' ' + k.K + ' ' + x.R + '">' +
      '<span class="pairk" lang="ja"><span class="' + (progress.learned.has(x.K) ? 'done' : '') + '">' + x.K + '</span>' +
      '<span class="kk' + (progress.learned.has(k.K) ? ' done' : '') + '">' + k.K + '</span></span><small>' + x.R + '</small></button>';
  }

  function categoryTabs() {
    return '<div class="seg">' + [['basic', '清音 · 46'], ['voiced', '浊音 · 25'], ['combo', '拗音 · 33']].map(function (c) {
      return '<button data-cat="' + c[0] + '" class="' + (category === c[0] ? 'active' : '') + '">' + c[1] + '</button>';
    }).join('') + '</div>';
  }

  function renderTable() {
    var sc = progress.script, both = sc === 'both';
    var gs = C.Groups.filter(function (g) { return g.type === catScope(category); });
    var title = tableMode === 'groups' ? '一次一行，稳稳记住。' : sc === 'kata' ? '从「ア」开始，认识片假名。' : both ? '一张表，两种写法。' : '从「あ」开始，读懂日语。';
    var sub = tableMode === 'groups' ? '推荐顺序：先学完平假名（元音 → 清音 → 浊音 / 半浊音 → 拗音），再按同样的顺序学片假名。' :
      sc === 'kata' ? '片假名和平假名读音完全相同，多用于外来语和拟声词。熟悉状态与平假名分开记。' :
        both ? '左边平假名，右边片假名；下划线表示这种写法已熟悉。点开格子可以分别标记。' : '点开一个假名，看读音、听发音，再标记熟悉。今天先学一行就好。';
    var html = heading(title, sub);
    html += '<div class="seg"><button data-mode="table" class="' + (tableMode === 'table' ? 'active' : '') + '">五十音表</button>' +
      '<button data-mode="groups" class="' + (tableMode === 'groups' ? 'active' : '') + '">分组学习</button></div>';
    html += scriptTabs();
    html += categoryTabs();
    if (tableMode === 'table') {
      var cols = category === 'combo' ? 3 : 5;
      var heads = cols === 3 ? ['ya', 'yu', 'yo'] : ['a', 'i', 'u', 'e', 'o'];
      html += '<div class="card chart"><div class="kgrid" style="grid-template-columns:var(--labw) repeat(' + cols + ',minmax(0,1fr))"><span></span>' +
        heads.map(function (h) { return '<span class="head">' + h + '</span>'; }).join('');
      gs.forEach(function (g) {
        html += '<span class="lab">' + g.name + '</span>';
        var cells = [];
        for (var c = 0; c < cols; c++) cells.push(null);
        g.items.forEach(function (x) { cells[x.column] = x; });
        cells.forEach(function (x) {
          if (x && both) { html += pairCell(x, cols === 3); return; }
          html += x ? '<button class="kcell' + (progress.learned.has(x.K) ? ' learned' : '') + (cols === 3 ? ' combo' : '') + '" data-kana="' + x.K + '" aria-label="' + x.K + ' ' + x.R + '">' +
            '<span class="kana" lang="ja">' + x.K + '</span><small>' + x.R + '</small></button>' : '<span class="empty">·</span>';
        });
      });
      html += '</div></div><div class="btns' + (both ? ' even' : '') + '">' + (both ?
        '<button class="btn" data-act="learn-category" data-script-target="hira">学平假名 →</button><button class="btn primary" data-act="learn-category" data-script-target="kata">学片假名 →</button>' :
        '<button class="btn primary block" data-act="learn-category">学习当前范围 →</button>') + '</div>';
      html += '<div class="notice">' + (category === 'basic' ? '「五十音」是表格名称；现代常用清音共 46 个。' + (both ? '' : '绿色 ✓ 表示已熟悉。') + (sc !== 'hira' ? '注意形近的 シ / ツ、ソ / ン。' : '') :
        category === 'voiced' ? '「゛」是浊点，「゜」是半浊点。じ/ぢ、ず/づ 通常同音，之后在词语中区别写法。' :
          '小写的ゃ・ゅ・ょ与前字合成一拍；きゃ（kya）和 きや（ki-ya）不同。') + '</div>';
    } else {
      var list = both ? gs.concat(C.Groups.filter(function (g) { return g.type === C.typeFor(category, 'kata'); })) : gs;
      html += '<div class="groups">' + list.map(function (g) {
        var known = g.items.filter(function (x) { return progress.learned.has(x.K); }).length;
        return '<button class="group" data-group="' + g.id + '"><h3>' + (both ? (g.script === 'kata' ? '片假名 · ' : '平假名 · ') + g.name : g.name) + '</h3><div class="chars" lang="ja">' +
          g.items.map(function (x) { return x.K; }).join(' ') + '</div><div class="bar"><i style="width:' + pct(known, g.items.length) + '%"></i></div>' +
          '<small class="hint">' + known + ' / ' + g.items.length + ' 已熟悉</small></button>';
      }).join('') + '</div>';
    }
    view.innerHTML = html;
  }

  // ---------- 学习范围（统一的底部选择面板，取代系统下拉框） ----------
  function scopeGroups() {
    function rows(script) {
      return C.Groups.filter(function (g) { return g.script === script; }).map(function (g) {
        return [g.id, g.name + '  ' + g.items.map(function (x) { return x.K; }).join('')];
      });
    }
    return [
      { title: '平假名', items: [['basic', '清音（46）'], ['voiced', '浊音 / 半浊音（25）'], ['combo', '拗音（33）'], ['all', '全部（104）']] },
      { title: '片假名', items: [['kbasic', '清音（46）'], ['kvoiced', '浊音 / 半浊音（25）'], ['kcombo', '拗音（33）'], ['kall', '全部（104）']] },
      { title: '综合', items: [['mix', '平片混合（208）'], ['errors', '待复习错题'], ['learned', '已熟悉假名（巩固）']] },
      { title: '平假名 · 按行', items: rows('hira') },
      { title: '片假名 · 按行', items: rows('kata') }
    ];
  }
  // 「平假名 · 清音（46）」这样的完整名称：按行与综合分组本身已经说明了类别，不再重复前缀
  function scopeOptionLabel(g, name) { return /按行|综合/.test(g.title) ? name : g.title + ' · ' + name; }
  function scopeLabel() {
    var out = '';
    scopeGroups().forEach(function (g) {
      g.items.forEach(function (o) { if (o[0] === scope) out = scopeOptionLabel(g, o[1]); });
    });
    return out || '选择学习范围';
  }
  function scopePicker() {
    return '<button class="picker-trigger" data-act="open-scope" aria-haspopup="dialog" aria-label="学习范围：' + scopeLabel() + '"><span>' + scopeLabel() + '</span></button>';
  }

  function resetDeck() { deck = C.deck(scope, progress, deckShuffled); cardIndex = 0; flipped = false; lastStrokeKana = null; }
  function startCards(s) { scope = s; resetDeck(); page = 'cards'; render(); window.scrollTo(0, 0); }
  function newQuiz(s) { return new C.Quiz(s, progress, null, quizMode); }
  function startQuiz(s) { scope = s; quiz = newQuiz(s); page = 'quiz'; render(); window.scrollTo(0, 0); }
  function moveCard(n) { if (!deck.length) return; cardIndex = (cardIndex + n + deck.length) % deck.length; flipped = false; render(); }

  // ---------- 记忆卡 ----------
  function cardModeTabs() {
    return '<div class="seg" role="tablist" aria-label="记忆卡类型"><button data-card-mode="kana" class="' + (cardMode === 'kana' ? 'active' : '') + '">假名</button>' +
      '<button data-card-mode="words" class="' + (cardMode === 'words' ? 'active' : '') + '">单词</button></div>';
  }

  // ---------- 单词卡（2.6.0）：只出用已熟悉的假名能读出来的词，没有配音 ----------
  function wordDeck() { return W.deck(progress, wordFilter); }
  function renderWords() {
    var html = heading('认识的假名，拼成单词。', '看着假名读出来，再翻开看中文意思。每多熟悉一个假名，就会解锁新的词。');
    html += cardModeTabs();
    var all = W.all(), canRead = W.deck(progress, 'all'), known = canRead.filter(function (w) { return progress.words.has(w.K); }).length;
    var next = Plan.nextGroup(progress), gain = W.unlockBy(progress, next);
    html += '<div class="wmeter"><div class="row"><span>能读的词</span><strong>' + canRead.length + '<span> / ' + all.length + '</span></strong></div>' +
      '<div class="bar green"><i style="width:' + pct(canRead.length, all.length) + '%"></i></div>' +
      '<div class="row hint"><span>已记住 ' + known + ' 个</span><span>' + (next && gain ? '再学「' + C.groupLabel(next) + '」可解锁 ' + gain + ' 个' : '') + '</span></div></div>';
    html += '<div class="chips" role="tablist" aria-label="单词范围">' + [['all', '全部'], ['hira', '平假名词'], ['kata', '片假名词'], ['todo', '还没记住']].map(function (c) {
      return '<button class="chip' + (wordFilter === c[0] ? ' on' : '') + '" data-word-filter="' + c[0] + '" aria-pressed="' + (wordFilter === c[0]) + '">' + c[1] + '</button>';
    }).join('') + '</div>';
    var d = wordDeck();
    if (!d.length) {
      html += '<div class="card empty-state"><strong>' + (canRead.length ? (wordFilter === 'todo' ? '能读的词都记住了' : '这个范围还没有能读的词') : '还没有能读的词') + '</strong>' +
        (canRead.length ? '换个范围看看，或者去学下一行假名，解锁更多单词。' : '先在「假名」卡里记住几个假名，比如「あ い う え お」，就能读出第一个词。') + '</div>';
    } else {
      if (wordIndex >= d.length) wordIndex = 0;
      var w = d[wordIndex], kata = w.script === 'kata';
      var back = '';
      if (wordFlipped) {
        back = '<div class="wback"><div class="answer">' + w.zh + '</div><div class="hint">' + (w.kanji ? '汉字写法 <b class="kanji">' + w.kanji + '</b> · ' : '') + w.romaji + '</div>';
        if (w.ex && W.canRead(w.ex, progress)) back += '<div class="wex"><span lang="ja">' + w.ex + '</span>　' + w.exZh + '</div>';
        W.notes(w).forEach(function (n) { back += '<div class="wnote">' + n + '</div>'; });
        back += '</div>';
      }
      html += '<div class="row hint"><span>单词卡 · ' + (kata ? '片假名词' : '平假名词') + (progress.words.has(w.K) ? ' · 已记住' : '') + '</span><span>' + (wordIndex + 1) + ' / ' + d.length + '</span></div>' +
        '<div class="bar" style="margin:6px 0 14px"><i style="width:' + ((wordIndex + 1) / d.length * 100) + '%"></i></div>' +
        '<button class="flash word-card" data-act="word-flip" aria-label="' + w.K + (wordFlipped ? '，' + w.zh : '，点击翻开') + '"><div class="wword' + (kata ? ' kk' : '') + '" lang="ja">' + w.K + '</div>' +
        (wordFlipped ? back : '<div class="hint">先读出来，再点卡片看中文意思</div>') + '</button>' +
        '<div class="btns even"><button class="btn outline" data-act="word-prev">上一张</button><button class="btn outline" data-act="word-next">下一张</button></div>';
      if (wordFlipped) html += '<div class="btns even"><button class="btn" data-act="word-again">还不熟</button><button class="btn primary" data-act="word-know">记住了 ✓</button></div>';
    }
    // 词表：能读的词全部列出；还不能读的只列接下来的 12 个，并标出还差哪几个假名
    var lockedList = W.locked(progress, wordFilter === 'todo' ? 'all' : wordFilter);
    html += '<section class="card wlist"><div class="row"><h2>词表</h2><span class="hint">' + d.length + ' 个能读</span></div>' +
      (d.length ? '<div class="wgrid">' + d.map(function (x, i) {
        return '<button class="witem' + (i === wordIndex ? ' cur' : '') + '" data-word="' + i + '"><span class="w' + (x.script === 'kata' ? ' kk' : '') + '" lang="ja">' + x.K + '</span><span class="m">' + x.zh + '</span>' +
          (progress.words.has(x.K) ? '<span class="st">✓</span>' : '') + '</button>';
      }).join('') + '</div>' : '') +
      (lockedList.length ? '<h3 class="wlocked-h">还不能读 · ' + lockedList.length + ' 个</h3><div class="wgrid">' + lockedList.slice(0, 12).map(function (x) {
        return '<div class="witem locked"><span class="w" lang="ja">' + x.K + '</span><span class="m">还差 ' + W.missing(x, progress).map(function (c) { return '<b lang="ja">' + c + '</b>'; }).join(' ') + '</span></div>';
      }).join('') + '</div>' + (lockedList.length > 12 ? '<p class="hint">另有 ' + (lockedList.length - 12) + ' 个词，学会更多假名后解锁。</p>' : '') : '') + '</section>';
    html += '<p class="hint small">词表选自 JLPT N5 词汇（Jonathan Waller，CC BY），中文释义为本应用编写。</p>';
    view.innerHTML = html;
  }
  function moveWord(n) { var d = wordDeck(); if (!d.length) return; wordIndex = (wordIndex + n + d.length) % d.length; wordFlipped = false; render(); }

  function renderCards() {
    if (cardMode === 'words') { renderWords(); return; }
    var html = heading('先回想，再翻开答案。', '看着假名试着读出来。点卡片核对答案，再决定是否熟悉。');
    html += cardModeTabs();
    html += '<div class="row" style="margin-bottom:12px">' + scopePicker() + '<button class="btn" data-act="shuffle">' + (deckShuffled ? '按五十音顺序' : '打乱顺序') + '</button></div>';
    if (!deck.length) {
      html += '<div class="card empty-state"><strong>' + (scope === 'learned' ? '还没有已熟悉的假名' : '当前范围没有可学习的假名') + '</strong>请切换学习范围。</div>';
      view.innerHTML = html; return;
    }
    var x = deck[cardIndex];
    html += '<div class="row hint"><span>记忆卡 · ' + (deckShuffled ? '已打乱' : '五十音顺序') + '</span><span>' + (cardIndex + 1) + ' / ' + deck.length + '</span></div>' +
      '<div class="bar" style="margin:6px 0 14px"><i style="width:' + ((cardIndex + 1) / deck.length * 100) + '%"></i></div>' +
      '<button class="flash" data-act="flip" aria-label="' + (flipped ? '隐藏答案' : '翻开答案') + '"><div class="big" lang="ja">' + x.K + '</div>' +
      '<div class="answer">' + (flipped ? x.R : '· · ·') + '</div><div class="hint">' + (flipped ? (x.script === 'kata' ? '平假名写作 ' : '片假名写作 ') + kanaSpan(x.pair) + ' · 再点一次可隐藏答案' : '点击卡片，核对读音') + '</div></button>' +
      '<div class="btns even"><button class="btn outline" data-act="listen-card">听发音</button><button class="btn outline" data-act="prev">上一张</button><button class="btn outline" data-act="next">下一张</button></div>';
    if (flipped) html += '<div class="btns even"><button class="btn" data-act="again">还不熟，再练练</button><button class="btn primary" data-act="know">我记住了 ✓</button></div>';
    if (Strokes.has(x.K)) {
      html += '<section class="card strokes" aria-label="笔顺"><header><h2>笔顺</h2><span>共 ' + Strokes.get(x.K).length + ' 画</span>' +
        '<button class="btn replay" data-act="stroke-replay">▶ 重播</button></header><button class="stroke-board" id="stroke-board" data-act="stroke-replay" aria-label="' + x.K + ' 的笔顺，点击重播"></button>' +
        '<div class="stroke-steps" id="stroke-steps"></div><p class="legend"><b></b>紫色是当前这一笔，圆点是起笔处，数字是第几画。点小格可以单独看某一笔。</p></section>';
    }
    html += '<button class="btn block" data-act="quiz-scope">用测验检查这一组 →</button>';
    view.innerHTML = html;
    if (Strokes.has(x.K)) {
      // 换到新卡片才自动书写；翻开答案时直接显示写完的样子
      Strokes.mount(x.K, x.K !== lastStrokeKana);
      lastStrokeKana = x.K;
    }
  }

  // ---------- 笔顺（2.3.0，数据来自 KanjiVG，CC BY-SA 3.0；与桌面版 StrokeData / StrokePanel 一致） ----------
  var lastStrokeKana = null;
  var Strokes = (function () {
    var NS = 'http://www.w3.org/2000/svg', W = 109, CW = 150, SMALL = { tx: 81, ty: 27, k: 0.72 };
    var data = window.KANA_STROKES || {}, timers = [];
    function has(k) { return !!k && k.split('').every(function (c) { return !!data[c]; }); }
    function get(k) {
      var out = [];
      k.split('').forEach(function (c, n) {
        (data[c] || []).forEach(function (d) { out.push({ d: d, tx: n ? SMALL.tx : 0, ty: n ? SMALL.ty : 0, k: n ? SMALL.k : 1 }); });
      });
      return out;
    }
    function vb(k) { return '0 0 ' + (k.length > 1 ? CW : W) + ' ' + W; }
    function el(tag, attrs, parent) {
      var e = document.createElementNS(NS, tag);
      Object.keys(attrs).forEach(function (a) { e.setAttribute(a, attrs[a]); });
      if (parent) parent.appendChild(e);
      return e;
    }
    function tf(s) { return 'translate(' + s.tx + ' ' + s.ty + ') scale(' + s.k + ')'; }
    function number(svg, p, s, i, current) {
      var a0 = p.getPointAtLength(0), b0 = p.getPointAtLength(Math.min(6, p.getTotalLength()));
      var a = { x: a0.x * s.k + s.tx, y: a0.y * s.k + s.ty }, b = { x: b0.x * s.k + s.tx, y: b0.y * s.k + s.ty };
      var len = Math.hypot(b.x - a.x, b.y - a.y) || 1;
      if (current) el('circle', { cx: a.x, cy: a.y, r: 2.6, 'class': 'sg-start' }, svg);
      var t = el('text', { x: a.x - (b.x - a.x) / len * 7, y: a.y - (b.y - a.y) / len * 7 + 2.5, 'text-anchor': 'middle', 'class': current ? 'sg-num' : 'sg-num old' }, svg);
      t.textContent = i + 1;
    }
    function board(k) {
      var box = document.getElementById('stroke-board');
      box.innerHTML = '';
      var svg = el('svg', { viewBox: vb(k), 'aria-hidden': 'true' }, box), w = k.length > 1 ? CW : W;
      el('line', { x1: w / 2, y1: 2, x2: w / 2, y2: W - 2, 'class': 'sg-guide' }, svg);
      el('line', { x1: 2, y1: W / 2, x2: w - 2, y2: W / 2, 'class': 'sg-guide' }, svg);
      return svg;
    }
    function drawUpTo(svg, list, upto, numbers) {
      list.forEach(function (s, i) {
        var p = el('path', { d: s.d, transform: tf(s), 'class': i < upto ? 'sg-done' : i === upto ? 'sg-now' : 'sg-ghost' }, svg);
        if (numbers && i <= upto) number(svg, p, s, i, i === upto);
      });
    }
    function mark(i) {
      var steps = document.querySelectorAll('.stroke-step');
      for (var n = 0; n < steps.length; n++) steps[n].setAttribute('aria-current', String(n === i));
    }
    function stop() { timers.forEach(clearTimeout); timers = []; }
    function finished(k) {
      stop();
      var list = get(k), svg = board(k);
      var paths = list.map(function (s) { return el('path', { d: s.d, transform: tf(s), 'class': 'sg-done' }, svg); });
      list.forEach(function (s, i) { number(svg, paths[i], s, i, false); });
      mark(-1);
    }
    function play(k) {
      stop();
      if (window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches) { finished(k); return; }
      var list = get(k), svg = board(k), t = 300;
      var paths = list.map(function (s) { return el('path', { d: s.d, transform: tf(s), 'class': 'sg-ghost' }, svg); });
      paths.forEach(function (p, i) {
        var L = p.getTotalLength(), dur = Math.max(380, Math.min(1100, L * 9));
        timers.push(setTimeout(function () {
          if (!document.body.contains(p)) return;
          mark(i);
          if (i > 0) paths[i - 1].setAttribute('class', 'sg-done');
          var olds = svg.querySelectorAll('.sg-start');
          for (var n = 0; n < olds.length; n++) olds[n].remove();
          var nums = svg.querySelectorAll('.sg-num');
          for (var m = 0; m < nums.length; m++) nums[m].setAttribute('class', 'sg-num old');
          number(svg, p, list[i], i, true);
          p.setAttribute('class', 'sg-now');
          p.style.strokeDasharray = L; p.style.strokeDashoffset = L;
          p.getBoundingClientRect();
          p.style.transition = 'stroke-dashoffset ' + dur + 'ms ease-in-out';
          p.style.strokeDashoffset = 0;
        }, t));
        t += dur + 260;
      });
      timers.push(setTimeout(function () { if (document.body.contains(svg)) finished(k); }, t));
    }
    function pin(k, i) {
      stop();
      var svg = board(k);
      drawUpTo(svg, get(k), i, true);
      mark(i);
    }
    function mount(k, autoplay) {
      var box = document.getElementById('stroke-steps'), list = get(k);
      box.innerHTML = '';
      list.forEach(function (_, i) {
        var b = document.createElement('button');
        b.className = 'stroke-step'; b.setAttribute('data-step', i); b.setAttribute('aria-label', '第 ' + (i + 1) + ' 画');
        drawUpTo(el('svg', { viewBox: vb(k), 'aria-hidden': 'true' }, b), list, i, false);
        var tag = document.createElement('i'); tag.textContent = i + 1; b.appendChild(tag);
        box.appendChild(b);
      });
      if (autoplay) play(k); else finished(k);
    }
    return { has: has, get: get, mount: mount, play: play, pin: pin, stop: stop };
  })();

  // ---------- 辨认测验 ----------
  function renderQuiz() {
    if (!quiz) quiz = newQuiz(scope);
    var q = quiz, pairMode = q.mode === 'pair';
    var html = pairMode ? heading('平假名和片假名，配成一对。', '看一种写法，选出另一种写法。成绩记在片假名上，答错的会进入复习。') :
      heading('这个假名，怎么读？', '选择正确的罗马音。答题后可以听发音；错题会自动进入复习。');
    html += '<div class="seg" role="tablist" aria-label="题型"><button data-quiz-mode="read" class="' + (pairMode ? '' : 'active') + '">看字选读音</button>' +
      '<button data-quiz-mode="pair" class="' + (pairMode ? 'active' : '') + '">平片配对</button></div>';
    html += '<div class="row" style="margin-bottom:12px">' + scopePicker() + '<button class="btn" data-act="restart-quiz">重来</button></div>';
    if (!q.items.length) {
      html += '<div class="card empty-state"><strong>' + (scope === 'learned' ? '还没有已熟悉的假名' : '当前没有待复习的错题') + '</strong>' +
        (scope === 'learned' ? '先用记忆卡学一行吧。' : '来一轮清音测验吧。') + '<div class="btns"><button class="btn primary block" data-act="basic-quiz">从清音开始测验</button></div></div>';
    } else if (q.done()) {
      html += '<div class="card empty-state"><strong>本轮完成！' + q.score + ' / ' + q.items.length + '</strong>' +
        (q.score === q.items.length ? '全部答对！明天再复习一次，让记忆更牢。' : '答错的假名已经加入复习，再见几次就会更熟悉。') +
        '<div class="btns even"><button class="btn primary" data-act="restart-quiz">再测一轮</button><button class="btn" data-page="errors">去错题复习</button></div></div>';
    } else {
      var x = q.current(), want = q.expected(), tgt = q.target();
      var dir = pairMode ? (x.script === 'hira' ? '平假名 → 片假名' : '片假名 → 平假名') : '假名 → 读音';
      var other = x.script === 'hira' ? '片假名' : '平假名';
      html += '<div class="row hint"><span>' + dir + '</span><span>' + (q.index + 1) + ' / ' + q.items.length + '</span></div>' +
        '<div class="bar" style="margin:6px 0 4px"><i style="width:' + (q.index / q.items.length * 100) + '%"></i></div>' +
        '<div class="center big" lang="ja">' + x.K + '</div><div class="choices' + (pairMode ? ' kana-choices' : '') + '">' + q.options.map(function (r) {
          var cls = q.submitted ? (r === want ? ' correct' : r === q.selected ? ' wrong' : '') : '';
          return '<button class="choice' + cls + '" data-answer="' + r + '"' + (pairMode ? ' lang="ja"' : '') + (q.submitted ? ' disabled' : '') + '>' + r + '</button>';
        }).join('') + '</div><div class="feedback" role="status">' + (q.submitted ? (q.lastRight() ?
          '✓ 答对了！' + (pairMode ? kanaSpan(x.K) + ' ↔ ' + kanaSpan(want) + '（' + x.R + '）。' : '') + (progress.errors[tgt.K] ? '还需再答对一次才能移出错题。' : '') :
          (pairMode ? '再记一次：' + kanaSpan(x.K) + ' 的' + other + '是 ' + kanaSpan(want) + '（' + x.R + '）。' + kanaSpan(tgt.K) + ' 已加入错题。' :
            '再记一次：' + kanaSpan(x.K) + ' 的读音是 <b>' + x.R + '</b>。已加入错题。')) : '先凭记忆作答。') + '</div>';
      if (q.submitted) html += '<div class="btns even"><button class="btn outline" data-act="listen-quiz">听发音</button><button class="btn primary" data-act="next-question">' +
        (q.index + 1 === q.items.length ? '查看结果' : '下一题 →') + '</button></div>';
    }
    view.innerHTML = html;
  }

  // ---------- 错题复习 ----------
  function renderErrors() {
    var es = C.pool('errors', progress);
    var html = heading('把不熟悉的，变成熟悉。', '答错或标记「还不熟」的假名会留在这里；连续答对两次后移出错题。');
    html += '<div class="card"><h2>待复习 · ' + es.length + ' 个</h2>';
    if (!es.length) {
      html += '<p class="hint">这里干干净净。来一轮测验，看看记住了多少。</p><div class="btns"><button class="btn primary block" data-act="basic-quiz">从清音开始测验</button></div>';
    } else {
      html += '<div class="btns even"><button class="btn" data-act="review-cards">先看记忆卡</button><button class="btn primary" data-act="review-quiz">开始错题测验 →</button></div>' +
        '<div class="errgrid">' + es.map(function (x) {
          return '<button class="erritem" data-kana="' + x.K + '"><b lang="ja">' + x.K + '</b>' + x.R + (x.script === 'kata' ? ' · 片' : '') + '<small>还需答对 ' + progress.errors[x.K] + ' 次</small></button>';
        }).join('') + '</div>';
    }
    html += '</div><div class="notice">仅翻卡或标记熟悉不会清除错题；需要通过测验答对。</div>';
    view.innerHTML = html;
  }

  // ---------- 学习指南 ----------
  function renderHelp() {
    view.innerHTML = '<div class="guide">' + heading('每天一小步，读懂新世界。', '面向中文母语、完全零基础、以阅读漫画和轻小说为目标的学习者。') +
      '<div class="card"><h2 style="margin-top:0">每天 10–15 分钟，先从 5 个假名开始</h2><ol>' +
      '<li>选一行：点开字形，看罗马音，听发音。</li><li>翻记忆卡：先尝试回想，再点开答案。</li>' +
      '<li>做一轮测验：每轮最多 10 题，小组不足 10 个时全部练习。</li><li>第二天先复习错题，再学新的一行。</li></ol></div>' +
      '<div class="card"><h2 style="margin-top:0">今日计划与专注计时</h2><p>「今日」页按你的进度给出当天建议：有错题先复习，再学下一行，做一轮测验，并完成每日专注时长。错题达到 8 个时，会建议当天先不学新内容。翻卡记住或测验答对都会计入「已熟悉」。</p>' +
      '<p>计时器在切换页面时不会停止；只有计时器走过的时间会计入学习时长。离开应用后，时间到会发通知提醒（首次使用时请允许通知）。</p></div>' +
      '<div class="card"><h2 style="margin-top:0">外观与护眼</h2><p>右上角 ◐ 可切换「亮色护眼」「深色护眼」或「跟随系统」。两套配色都用暖色纸张底，不用纯白纯黑，正文对比度适中；长时间学习时，也建议每 20 分钟看看远处，让眼睛休息一下。</p>' +
      '<p class="small">当前：' + THEME_INFO[progress.theme].name + (progress.theme === 'system' ? '（' + (systemDark ? '深色' : '亮色') + '）' : '') + '</p><button class="btn" data-act="theme">更改外观</button></div>' +
      '<div class="card"><h2 style="margin-top:0">认读时要注意</h2><p>罗马音是临时提示，不是中文拼音。し读 shi、ち读 chi、つ读 tsu、ふ读 fu；ら行不等于英语 r。を通常读 o；は、へ作助词时读 wa、e。</p>' +
      '<p>先学 46 个清音，再学浊音、半浊音和常用拗音。平假名都熟悉后，「今日」会接着推荐片假名；之后还要学习词汇与语法，才能逐步阅读喜欢的故事。</p></div>' +
      '<div class="card"><h2 style="margin-top:0">片假名</h2><p>片假名和平假名读音完全相同，多用于外来语（コーヒー 咖啡）、拟声词和强调。五十音表顶部可以切换「平假名 / 片假名 / 平片对照」；两种写法的熟悉状态分开记。</p>' +
      '<p>测验里的「平片配对」会给出一种写法，让你选出另一种写法，专门练两者的对应。注意形近的 シ / ツ、ソ / ン。</p></div>' +
      '<div class="card"><h2 style="margin-top:0">单词卡</h2><p>「记忆卡」顶部切换到「单词」，就能用学过的假名读真正的日语单词。单词卡只出你已经熟悉的假名能拼出来的词，每多熟悉一个假名就会解锁新的词；背面有中文意思、汉字写法，部分词附一个常用搭配。单词卡没有配音，读音靠你刚学会的假名。</p>' +
      '<p class="small">词表：选自 JLPT N5 词汇表，Jonathan Waller（https://www.tanos.co.uk/jlpt/），授权 CC BY；经 open-anki-jlpt-decks 整理。中文释义、汉字写法说明和搭配为本应用编写。</p></div>' +
      '<div class="card"><h2 style="margin-top:0">使用与进度</h2><p>104 个发音片段内置在应用中（片假名与平假名共用），无需联网。进度自动保存在本机，卸载应用会删除进度。手机版与电脑版的进度各自独立。</p>' +
      '<p class="small">版本 ' + C.VERSION + (call('dataPath') ? ' · 进度文件：' + call('dataPath') : '') + '</p></div>' +
      '<div class="card"><h2 style="margin-top:0">录音来源与授权</h2><p class="small">录音：FUN Japanese Learning（https://funjapaneselearning.com）。<br>数据集整理：Arsalan Anwari，Kana Sounds。<br>https://huggingface.co/datasets/arsalan-anwari/kana-sounds<br>' +
      '授权：Creative Commons Attribution 4.0（CC BY 4.0）。<br>https://creativecommons.org/licenses/by/4.0/<br>录音按原样内置；调整了文件标识以匹配本应用的罗马音显示。</p></div>' +
      '<div class="card"><h2 style="margin-top:0">笔顺与字体</h2><p class="small">笔顺数据：KanjiVG（https://kanjivg.tagaini.net），Copyright (C) 2009/2010/2011 Ulrich Apel。<br>授权：Creative Commons Attribution-ShareAlike 3.0（CC BY-SA 3.0）。<br>https://creativecommons.org/licenses/by-sa/3.0/<br>' +
      '假名字体：Klee One，Copyright 2020 The Klee Project Authors。<br>授权：SIL Open Font License 1.1（https://openfontlicense.org）；本应用只内置了其中的假名部分。</p></div></div>';
  }

  // ---------- 假名详情（底部面板） ----------
  function openSheet(html) {
    $('sheet').classList.remove('picker');
    $('sheet').innerHTML = html;
    $('sheet').classList.remove('hidden');
    $('backdrop').classList.remove('hidden');
  }
  // 选择面板（学习范围、每日目标）：与外观面板同一套卡片样式，选中即生效并关闭
  function openPicker(html) {
    openSheet(html);
    $('sheet').classList.add('picker');
  }
  function pickItem(on, value, label, attr) {
    return '<button class="pk-item' + (on ? ' active' : '') + '" ' + attr + '="' + value + '" aria-pressed="' + on + '">' + label +
      '<span class="pk-tick">' + (on ? '✓' : '') + '</span></button>';
  }
  function openScopeSheet() {
    openPicker('<h2 class="pk-head">学习范围</h2><p class="hint pk-sub">选择要学习或测验的假名</p>' +
      scopeGroups().map(function (g) {
        return '<div class="pk-title">' + g.title + '</div><div class="pk-list">' + g.items.map(function (o) {
          return pickItem(scope === o[0], o[0], o[1], 'data-scope-opt');
        }).join('') + '</div>';
      }).join(''));
  }
  function openGoalSheet() {
    openPicker('<h2 class="pk-head">每日目标</h2><p class="hint pk-sub">每天专注学习的分钟数，进度条按此计算</p><div class="pk-list">' +
      C.GOAL_CHOICES.map(function (g) { return pickItem(g === progress.goal, g, g + ' 分钟', 'data-goal-opt'); }).join('') + '</div>');
  }
  function setScope(s) {
    closeSheet();
    scope = s;
    if (page === 'cards') { resetDeck(); render(); } else { startQuiz(s); }
  }
  function setGoal(g) { closeSheet(); progress.goal = g; save(); render(); }
  function closeSheet() {
    $('sheet').classList.add('hidden');
    $('backdrop').classList.add('hidden');
    sheetKana = null;
  }
  // 详情面板同时展示两种写法，可以分别标记熟悉（2.5.0）
  function showKana(k) {
    var x = C.ByKana[k];
    if (!x) return;
    sheetKana = k;
    var h = x.script === 'hira' ? x : C.ByKana[x.pair], kt = C.ByKana[h.pair];
    var hint = C.note(x), extra = x.script === 'hira' ? C.kataHint(x) : '';
    function glyph(y, label) {
      return '<div class="pg' + (y === x ? ' on' : '') + '"><div class="big' + (y.script === 'kata' ? ' kk' : '') + '" lang="ja">' + y.K + '</div><small>' + label + (progress.learned.has(y.K) ? ' · 已熟悉' : '') + '</small></div>';
    }
    function mark(y, label) {
      var on = progress.learned.has(y.K);
      return '<button class="btn' + (on ? ' known' : '') + '" data-mark="' + y.K + '">' + (on ? '✓ ' + label + '已熟悉' : '标记' + label + '熟悉') + '</button>';
    }
    openSheet('<div class="hint">' + C.groupLabel(C.groupById(x.group)) + '</div><div class="pairbig">' + glyph(h, '平假名') + glyph(kt, '片假名') + '</div>' +
      '<div class="answer">' + x.R + '</div><p class="hint" style="margin:6px 0 ' + (extra ? '6px' : '14px') + '">' + hint + '</p>' +
      (extra ? '<p class="hint tip">' + extra + '</p>' : '') +
      '<div class="btns even">' + mark(h, '平假名') + mark(kt, '片假名') + '</div>' +
      '<button class="btn outline block" data-act="listen-sheet">听发音</button>');
  }

  // ---------- 事件 ----------
  document.addEventListener('click', function (e) {
    if (e.target.id === 'backdrop') { closeSheet(); return; }
    var b = e.target.closest('button');
    if (!b || b.disabled) return;
    var d = b.dataset;
    if (d.page) { closeSheet(); navigate(d.page); return; }
    if (d.task !== undefined) { runTask(window.__tasks[Number(d.task)]); return; }
    if (d.preset) { setPreset(Number(d.preset)); return; }
    if (d.themeMode) { setTheme(d.themeMode); return; }
    if (d.scopeOpt) { setScope(d.scopeOpt); return; }
    if (d.goalOpt) { setGoal(Number(d.goalOpt)); return; }
    if (d.step !== undefined && deck[cardIndex]) { Strokes.pin(deck[cardIndex].K, Number(d.step)); return; }
    if (d.cat) { category = d.cat; render(); return; }
    if (d.cardMode) { if (cardMode !== d.cardMode) { cardMode = d.cardMode; wordFlipped = false; render(); window.scrollTo(0, 0); } return; }
    if (d.wordFilter) { wordFilter = d.wordFilter; wordIndex = 0; wordFlipped = false; render(); return; }
    if (d.word !== undefined) { wordIndex = Number(d.word); wordFlipped = false; render(); window.scrollTo({ top: 0, behavior: 'smooth' }); return; }
    if (d.script) { progress.script = d.script; save(); render(); return; }
    if (d.quizMode) { if (quizMode !== d.quizMode) { quizMode = d.quizMode; quiz = newQuiz(scope); render(); } return; }
    if (d.mark) { var mk = sheetKana; progress.toggleLearned(C.ByKana[d.mark]); save(); render(); showKana(mk); return; }
    if (d.mode) { tableMode = d.mode; render(); return; }
    if (d.kana) { showKana(d.kana); return; }
    if (d.group) { startCards(d.group); return; }
    if (d.answer) {
      if (quiz && quiz.answer(d.answer)) {
        vibrate(quiz.lastRight() ? 15 : 60);
        save(); render();
        // 屏幕较矮或字号较大时，「听发音 / 下一题」可能落在底部标签栏后面：答题后把它滚到可见处
        // （不用 scrollIntoView：部分 WebView 在元素已部分可见时不会滚动，这里直接算出要滚多少）
        var nextBtn = view.querySelector('[data-act="next-question"]');
        if (nextBtn) {
          var tabTop = document.getElementById('tabbar').getBoundingClientRect().top;
          var need = nextBtn.getBoundingClientRect().bottom - (tabTop - 12);
          if (need > 0) window.scrollBy({ top: need, behavior: 'smooth' });
        }
      }
      return;
    }
    var x = deck[cardIndex];
    switch (d.act) {
      case 'timer-toggle': toggleTimer(); break;
      case 'timer-add': addFive(); break;
      case 'timer-reset': resetTimer(); break;
      case 'learn-category': startCards(d.scriptTarget ? C.typeFor(category, d.scriptTarget) : catScope(category)); break;
      case 'shuffle': deckShuffled = !deckShuffled; resetDeck(); render(); break;
      case 'flip': flipped = !flipped; render(); break;
      case 'prev': moveCard(-1); break;
      case 'next': moveCard(1); break;
      case 'listen-card': if (x) speak(x); break;
      case 'know': if (x) { progress.cardKnown(x); save(); moveCard(1); } break;
      case 'again': if (x) { progress.cardAgain(x); save(); moveCard(1); toast('已加入错题，之后再来复习。'); } break;
      case 'quiz-scope': startQuiz(scope); break;
      case 'restart-quiz': startQuiz(scope); break;
      case 'basic-quiz': quizMode = 'read'; startQuiz('basic'); break;
      case 'next-question': if (quiz && quiz.submitted) { quiz.next(); render(); window.scrollTo(0, 0); } break;
      case 'listen-quiz': if (quiz && quiz.current()) speak(quiz.current()); break;
      case 'review-cards': startCards('errors'); break;
      case 'review-quiz': startQuiz('errors'); break;
      case 'listen-sheet': if (sheetKana) speak(C.ByKana[sheetKana]); break;
      case 'close-sheet': closeSheet(); break;
      case 'word-flip': wordFlipped = !wordFlipped; render(); break;
      case 'word-prev': moveWord(-1); break;
      case 'word-next': moveWord(1); break;
      case 'word-know': case 'word-again': {
        var wd = wordDeck(), cur = wd[wordIndex];
        if (cur) {
          if (d.act === 'word-know') progress.wordKnown(cur); else progress.wordAgain(cur);
          save();
          // 「还没记住」范围里记住一个词后，它会移出牌组：保持当前位置即可看到下一个
          if (!(wordFilter === 'todo' && d.act === 'word-know')) wordIndex++;
          var len = wordDeck().length; if (wordIndex >= len) wordIndex = 0;
          wordFlipped = false; render();
        }
        break;
      }
      case 'theme': openThemeSheet(); break;
      case 'open-scope': openScopeSheet(); break;
      case 'open-goal': openGoalSheet(); break;
      case 'stroke-replay': if (x) Strokes.play(x.K); break;
    }
  });

  window.addEventListener('scroll', function () {
    document.querySelector('.top').classList.toggle('scrolled', window.scrollY > 4);
  }, { passive: true });

  document.addEventListener('visibilitychange', function () {
    onTick();
    if (document.hidden) { unsaved = 0; save(); }
    else if (page === 'today') render();
  });

  // 供安卓原生层调用
  window.kanaBack = function () {
    if (!$('sheet').classList.contains('hidden')) { closeSheet(); return true; }
    if (page !== 'today') { navigate('today'); return true; }
    return false;
  };
  // 系统深色模式变化（安卓由原生层通知；浏览器调试时监听媒体查询）
  window.kanaSystemTheme = function (dark) {
    systemDark = !!dark;
    if (progress.theme === 'system') { applyTheme(true); if (page === 'help') render(); }
  };
  if (!bridge && window.matchMedia) {
    var mq = window.matchMedia('(prefers-color-scheme: dark)');
    if (mq.addEventListener) mq.addEventListener('change', function (e) { window.kanaSystemTheme(e.matches); });
  }
  window.kanaPause = function () { onTick(); unsaved = 0; save(); };
  window.kanaResume = function () { onTick(); if (page === 'today') render(); };

  systemDark = readSystemDark();
  applyTheme(false);
  setInterval(onTick, 500);
  if (timer.running) scheduleAlarm();
  render();
})();
