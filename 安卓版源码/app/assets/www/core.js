/*
 * 假名小步 · 安卓版 —— 核心逻辑（与桌面版 2.1.1 的 C# 源码一一对应，无界面依赖）
 *   Data       ← src/Core/Data.cs
 *   Progress   ← src/Core/Progress.cs（存储格式改为 JSON）
 *   Quiz       ← src/Core/Quiz.cs
 *   StudyTimer ← src/Core/StudyTimer.cs
 *   DailyPlan  ← src/Core/DailyPlan.cs
 * 修改学习规则时，请同步修改桌面版对应文件，并在两边的自检中补充测试。
 */
(function (root) {
  'use strict';

  // ---------------- Data ----------------
  var ROWS = [
    'a|元音|あいうえお|a i u e o', 'k|か行|かきくけこ|ka ki ku ke ko', 's|さ行|さしすせそ|sa shi su se so',
    't|た行|たちつてと|ta chi tsu te to', 'n|な行|なにぬねの|na ni nu ne no', 'h|は行|はひふへほ|ha hi fu he ho',
    'm|ま行|まみむめも|ma mi mu me mo', 'y|や行|や ゆ よ|ya - yu - yo', 'r|ら行|らりるれろ|ra ri ru re ro',
    'w|わ行|わ   を|wa - - - wo', 'nn|拨音|ん    |n - - - -', 'g|が行|がぎぐげご|ga gi gu ge go',
    'z|ざ行|ざじずぜぞ|za ji zu ze zo', 'd|だ行|だぢづでど|da ji zu de do', 'b|ば行|ばびぶべぼ|ba bi bu be bo',
    'p|ぱ行|ぱぴぷぺぽ|pa pi pu pe po'
  ];
  var COMBOS = [
    'ky|きゃ きゅ きょ|kya kyu kyo', 'sh|しゃ しゅ しょ|sha shu sho', 'ch|ちゃ ちゅ ちょ|cha chu cho',
    'ny|にゃ にゅ にょ|nya nyu nyo', 'hy|ひゃ ひゅ ひょ|hya hyu hyo', 'my|みゃ みゅ みょ|mya myu myo',
    'ry|りゃ りゅ りょ|rya ryu ryo', 'gy|ぎゃ ぎゅ ぎょ|gya gyu gyo', 'j|じゃ じゅ じょ|ja ju jo',
    'by|びゃ びゅ びょ|bya byu byo', 'py|ぴゃ ぴゅ ぴょ|pya pyu pyo'
  ];

  function buildGroups() {
    var list = [];
    ROWS.forEach(function (row, i) {
      var f = row.split('|'), roma = f[3].split(' ');
      var g = { id: f[0], name: f[1], type: i < 11 ? 'basic' : 'voiced', items: [] };
      for (var j = 0; j < f[2].length; j++) {
        if (f[2][j] !== ' ') g.items.push({ K: f[2][j], R: roma[j], group: g.id, type: g.type, column: j });
      }
      list.push(g);
    });
    COMBOS.forEach(function (row) {
      var f = row.split('|'), ks = f[1].split(' '), rs = f[2].split(' ');
      var g = { id: f[0], name: ks[0] + '组', type: 'combo', items: [] };
      ks.forEach(function (k, l) { g.items.push({ K: k, R: rs[l], group: g.id, type: 'combo', column: l }); });
      list.push(g);
    });
    return list;
  }

  var Groups = buildGroups();
  var All = [];
  Groups.forEach(function (g) { All = All.concat(g.items); });
  var ByKana = {};
  All.forEach(function (k) { ByKana[k.K] = k; });
  function groupById(id) { for (var i = 0; i < Groups.length; i++) if (Groups[i].id === id) return Groups[i]; return null; }

  function pool(scope, p) {
    if (scope === 'errors') return All.filter(function (k) { return p.errors[k.K] > 0; });
    if (scope === 'learned') return All.filter(function (k) { return p.learned.has(k.K); });
    if (scope === 'all') return All.slice();
    var out = [];
    Groups.forEach(function (g) { if (g.id === scope || g.type === scope) out = out.concat(g.items); });
    return out;
  }

  function note(x) {
    if (x.K === 'を') return '通常读 o；罗马音写作 wo，用于区别「お」。';
    if (x.K === 'は') return '通常读 ha；作为助词时读 wa。';
    if (x.K === 'へ') return '通常读 he；作为助词时读 e。';
    if (x.K === 'ん') return '拨音，占一拍；实际发音会随后续音变化。';
    if (x.K === 'ぢ' || x.K === 'づ') return '现代标准日语中，ぢ通常与じ同音，づ通常与ず同音。';
    if (x.K.length > 1) return '后一个字是小写的ゃ、ゅ或ょ；两个字合起来占一拍。';
    return '观察字形，再听发音。罗马音只是辅助提示，不按中文拼音读。';
  }

  /** 内置录音文件名（与桌面版 OfflineAudio.Key 相同，去掉 KanaAudio. 前缀）。 */
  function audioKey(x) {
    var folder = x.type === 'basic' ? 'seion' : x.type === 'combo' ? 'yoon' : x.group === 'p' ? 'handakuon' : 'dakuon';
    var r = x.R;
    if (x.K === 'ぢ') r = 'di';
    if (x.K === 'づ') r = 'du';
    if (x.group === 'sh') r = 'sy' + x.R.slice(-1);
    if (x.group === 'ch') r = 'cy' + x.R.slice(-1);
    if (x.group === 'j') r = 'zy' + x.R.slice(-1);
    return folder + '.' + r;
  }

  function shuffle(source, rng) {
    rng = rng || Math.random;
    var a = source.slice();
    for (var i = a.length - 1; i > 0; i--) {
      var j = Math.floor(rng() * (i + 1));
      var t = a[i]; a[i] = a[j]; a[j] = t;
    }
    return a;
  }

  // 记忆卡牌组（2.4.0）：默认与五十音表相同的顺序（按行，行内按 a i u e o），shuffled 时打乱。
  function deck(scope, p, shuffled, rng) {
    var list = pool(scope, p).sort(function (a, b) { return All.indexOf(a) - All.indexOf(b); });
    return shuffled ? shuffle(list, rng) : list;
  }

  // ---------------- Progress ----------------
  var GOAL_CHOICES = [5, 10, 15, 20, 30, 45, 60];
  var TIMER_CHOICES = [5, 10, 15, 25];
  var KEEP_DAYS = 120;
  var THEMES = ['system', 'light', 'dark'];

  function pad(n) { return (n < 10 ? '0' : '') + n; }
  function dayKey(d) { return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); }
  function addDays(d, n) { var x = new Date(d.getFullYear(), d.getMonth(), d.getDate()); x.setDate(x.getDate() + n); return x; }
  function num(v, max) { v = Number(v); if (!isFinite(v) || v < 0) v = 0; return max ? Math.min(max, v) : v; }
  function newDay() { return { sec: 0, cards: 0, answered: 0, correct: 0, learned: 0, group: null }; }
  function dayActive(d) { return d.sec >= 30 || d.cards > 0 || d.answered > 0 || d.learned > 0; }

  function Progress() {
    this.learned = new Set();
    this.errors = {};
    this.answered = 0;
    this.correct = 0;
    this.goal = 15;
    this.timerMinutes = 15;
    this.days = {};
    this.timerState = null; // 计时器状态（安卓进程可能被系统回收，用于恢复）
    this.theme = 'system';  // 外观：system 跟随系统 / light 亮色护眼 / dark 深色护眼（2.2.0）
    this.clock = function () { return new Date(); };
  }

  Progress.FILE_VERSION = 1;

  /** 从 JSON 对象恢复，逐项校验；无效内容被忽略。 */
  Progress.fromJSON = function (o) {
    var p = new Progress();
    if (!o || typeof o !== 'object') return p;
    p.answered = Math.floor(num(o.answered));
    p.correct = Math.min(p.answered, Math.floor(num(o.correct)));
    if (GOAL_CHOICES.indexOf(o.goal) >= 0) p.goal = o.goal;
    if (o.timerMinutes >= 1 && o.timerMinutes <= 120) p.timerMinutes = Math.floor(o.timerMinutes);
    if (THEMES.indexOf(o.theme) >= 0) p.theme = o.theme;
    (Array.isArray(o.learned) ? o.learned : []).forEach(function (k) { if (ByKana[k]) p.learned.add(k); });
    if (o.errors && typeof o.errors === 'object') {
      Object.keys(o.errors).forEach(function (k) {
        var n = Math.floor(num(o.errors[k]));
        if (ByKana[k] && n > 0) p.errors[k] = Math.min(2, n);
      });
    }
    if (o.days && typeof o.days === 'object') {
      Object.keys(o.days).forEach(function (k) {
        var v = o.days[k];
        if (!/^\d{4}-\d{2}-\d{2}$/.test(k) || !v || typeof v !== 'object') return;
        var d = newDay();
        d.sec = num(v.sec, 86400);
        d.cards = Math.floor(num(v.cards));
        d.answered = Math.floor(num(v.answered));
        d.correct = Math.min(d.answered, Math.floor(num(v.correct)));
        d.learned = Math.floor(num(v.learned));
        if (typeof v.group === 'string' && groupById(v.group)) d.group = v.group;
        p.days[k] = d;
      });
    }
    var t = o.timerState;
    if (t && t.total >= 60 && t.total <= StudyTimer.MAX_SECONDS) {
      p.timerState = { total: t.total, remaining: num(t.remaining, t.total), running: !!t.running, endAt: num(t.endAt) };
    }
    return p;
  };

  Progress.prototype.toJSON = function () {
    var self = this, days = {};
    Object.keys(this.days).sort().slice(-KEEP_DAYS).forEach(function (k) {
      var d = self.days[k];
      if (d.sec < 1 && !d.cards && !d.answered && !d.learned && !d.group) return;
      days[k] = { sec: Math.round(d.sec), cards: d.cards, answered: d.answered, correct: d.correct, learned: d.learned };
      if (d.group) days[k].group = d.group;
    });
    var learned = All.filter(function (k) { return self.learned.has(k.K); }).map(function (k) { return k.K; });
    return {
      app: 'kana-small-steps', version: Progress.FILE_VERSION,
      answered: this.answered, correct: this.correct, goal: this.goal, timerMinutes: this.timerMinutes, theme: this.theme,
      learned: learned, errors: this.errors, days: days, timerState: this.timerState
    };
  };

  Progress.prototype.day = function (date) {
    var k = dayKey(date);
    return this.days[k] || (this.days[k] = newDay());
  };
  Progress.prototype.today = function () { return this.day(this.clock()); };
  Progress.prototype.studiedOn = function (date) { var d = this.days[dayKey(date)]; return !!d && dayActive(d); };

  /** 连续学习天数：今天还没学时，从昨天开始往前数。 */
  Progress.prototype.streak = function () {
    var d = addDays(this.clock(), 0);
    if (!this.studiedOn(d)) d = addDays(d, -1);
    var n = 0;
    while (this.studiedOn(d) && n < 3660) { n++; d = addDays(d, -1); }
    return n;
  };

  Progress.prototype.hasEarlierDays = function () {
    var today = dayKey(this.clock()), self = this;
    return Object.keys(this.days).some(function (k) { return k < today && dayActive(self.days[k]); });
  };

  /** 近 7 天正确率（至少 10 题），不足时用累计正确率（至少 20 题），都不足返回 -1。 */
  Progress.prototype.recentAccuracy = function () {
    var a = 0, c = 0, base = this.clock();
    for (var i = 0; i < 7; i++) {
      var d = this.days[dayKey(addDays(base, -i))];
      if (d) { a += d.answered; c += d.correct; }
    }
    if (a >= 10) return c / a;
    if (this.answered >= 20) return this.correct / this.answered;
    return -1;
  };

  Progress.prototype.addStudySeconds = function (s) {
    if (s > 0) { var d = this.today(); d.sec = Math.min(86400, d.sec + s); }
  };

  Progress.prototype.markAgain = function (x) { this.learned.delete(x.K); this.errors[x.K] = 2; };

  Progress.prototype.cardKnown = function (x) {
    var d = this.today();
    d.cards++;
    if (!this.learned.has(x.K)) { this.learned.add(x.K); d.learned++; }
  };

  Progress.prototype.cardAgain = function (x) { this.today().cards++; this.markAgain(x); };

  Progress.prototype.toggleLearned = function (x) {
    var d = this.today();
    if (this.learned.has(x.K)) { this.learned.delete(x.K); d.learned = Math.max(0, d.learned - 1); }
    else { this.learned.add(x.K); d.learned++; }
  };

  Progress.prototype.record = function (x, right) {
    var d = this.today();
    this.answered++;
    d.answered++;
    if (right) {
      if (this.correct < this.answered) this.correct++;
      if (d.correct < d.answered) d.correct++;
      if (this.errors[x.K]) {
        this.errors[x.K]--;
        if (this.errors[x.K] <= 0) delete this.errors[x.K];
      }
      // 2.1.1：答对且没有待复习次数时，视为已熟悉
      if (!this.errors[x.K] && !this.learned.has(x.K)) { this.learned.add(x.K); d.learned++; }
    } else {
      this.markAgain(x);
    }
  };

  Progress.prototype.errorCount = function () { return Object.keys(this.errors).length; };

  // ---------------- Quiz ----------------
  function Quiz(scope, p, rng) {
    this.scope = scope;
    this.state = p;
    this.rng = rng || Math.random;
    this.items = shuffle(pool(scope, p), this.rng).slice(0, 10);
    this.index = 0;
    this.score = 0;
    this.submitted = false;
    this.selected = null;
    this.setOptions();
  }
  Quiz.prototype.done = function () { return this.index >= this.items.length; };
  Quiz.prototype.current = function () { return this.done() ? null : this.items[this.index]; };
  Quiz.prototype.setOptions = function () {
    if (this.done()) { this.options = []; return; }
    var cur = this.current(), rng = this.rng;
    var source = shuffle(pool(this.scope, this.state), rng)
      .concat(shuffle(pool(cur.type, this.state), rng))
      .concat(shuffle(All, rng));
    var picked = [];
    for (var i = 0; i < source.length && picked.length < 3; i++) {
      var r = source[i].R;
      if (r !== cur.R && picked.indexOf(r) < 0) picked.push(r);
    }
    this.options = shuffle(picked.concat([cur.R]), rng);
  };
  Quiz.prototype.answer = function (r) {
    if (this.done() || this.submitted || this.options.indexOf(r) < 0) return false;
    this.submitted = true;
    this.selected = r;
    var right = r === this.current().R;
    if (right) this.score++;
    this.state.record(this.current(), right);
    return true;
  };
  Quiz.prototype.next = function () {
    if (!this.done() && this.submitted) {
      this.index++;
      this.submitted = false;
      this.selected = null;
      this.setOptions();
    }
  };

  // ---------------- StudyTimer ----------------
  /** 专注倒计时：按墙上时钟计算；单次 tick 最多计入 90 秒（避免睡眠 / 后台虚增）。时间单位：毫秒时间戳、秒。 */
  function StudyTimer(minutes) {
    this.total = StudyTimer.clamp(minutes * 60);
    this.remaining = this.total;
    this.running = false;
    this.endAt = 0;
    this.lastTick = 0;
  }
  StudyTimer.MAX_SECONDS = 7200;
  StudyTimer.MAX_CREDIT = 90;
  StudyTimer.clamp = function (s) { return Math.max(60, Math.min(StudyTimer.MAX_SECONDS, s)); };
  StudyTimer.format = function (s) {
    var t = Math.ceil(Math.max(0, s));
    return pad(Math.floor(t / 60)) + ':' + pad(t % 60);
  };
  StudyTimer.prototype.started = function () { return this.running || (this.remaining > 0 && this.remaining < this.total); };
  StudyTimer.prototype.finished = function () { return !this.running && this.remaining <= 0; };
  StudyTimer.prototype.elapsed = function () { return this.total > 0 ? 1 - this.remaining / this.total : 0; };
  StudyTimer.prototype.set = function (minutes) {
    if (this.running) return false;
    this.total = StudyTimer.clamp(minutes * 60);
    this.remaining = this.total;
    return true;
  };
  StudyTimer.prototype.start = function (now) {
    if (this.running) return;
    if (this.remaining <= 0) this.remaining = this.total;
    this.running = true;
    this.endAt = now + this.remaining * 1000;
    this.lastTick = now;
  };
  StudyTimer.prototype.pause = function (now) {
    var r = this.tick(now);
    this.running = false;
    return r.credit;
  };
  StudyTimer.prototype.reset = function () { this.running = false; this.remaining = this.total; };
  StudyTimer.prototype.addSeconds = function (s) {
    var nt = Math.min(StudyTimer.MAX_SECONDS, this.total + s), added = nt - this.total;
    this.total = nt;
    this.remaining = Math.min(this.total, this.remaining + added);
    if (this.running) this.endAt += added * 1000;
  };
  StudyTimer.prototype.tick = function (now) {
    if (!this.running) { this.lastTick = now; return { credit: 0, finished: false }; }
    var delta = Math.max(0, Math.min((now - this.lastTick) / 1000, StudyTimer.MAX_CREDIT));
    this.lastTick = now;
    var before = this.remaining;
    this.remaining = Math.max(0, (this.endAt - now) / 1000);
    var credit = Math.min(delta, before), finished = false;
    if (this.remaining <= 0) { this.remaining = 0; this.running = false; finished = true; }
    return { credit: credit, finished: finished };
  };
  StudyTimer.prototype.snapshot = function () {
    return { total: this.total, remaining: this.remaining, running: this.running, endAt: this.endAt };
  };
  /** 从保存的状态恢复；离开期间的时间不计入学习时长。 */
  StudyTimer.restore = function (s, fallbackMinutes, now) {
    var t = new StudyTimer(fallbackMinutes);
    if (!s) return t;
    t.total = StudyTimer.clamp(s.total);
    t.remaining = Math.min(t.total, Math.max(0, s.remaining));
    if (s.running) {
      if (s.endAt > now) { t.running = true; t.endAt = s.endAt; t.lastTick = now; t.remaining = (s.endAt - now) / 1000; }
      else { t.remaining = 0; }
    }
    return t;
  };

  // ---------------- DailyPlan ----------------
  var HEAVY_REVIEW = 8;
  var TIPS = [
    '把假名大声读出来，比默看记得更牢。',
    '把容易混淆的假名放在一起对比：さ / ち、ぬ / め、る / ろ、わ / れ / ね。',
    '学新的一行之前，先花两分钟复习昨天的内容。',
    '记忆卡想不起来时，先停三秒再翻面——努力回想本身就在加深记忆。',
    '用手指在桌上描一描字形，动作记忆能帮你认字。',
    '在身边找假名：包装、歌词、动画片头，每认出一个都是练习。',
    '短而频繁胜过长而偶尔：每天 15 分钟，比周末一次两小时更有效。',
    '测验答错不要紧，错题会自动回来找你，这就是间隔复习。',
    '「こんにちは」最后的は读 wa——は作助词时就是这样读。',
    '带着一点「意犹未尽」结束，明天会更想回来。'
  ];

  function dayOfYear(d) { return Math.round((new Date(d.getFullYear(), d.getMonth(), d.getDate()) - new Date(d.getFullYear(), 0, 0)) / 864e5); }

  var DailyPlan = {
    HEAVY_REVIEW: HEAVY_REVIEW,
    TIPS: TIPS,
    tipOfDay: function (d) { return TIPS[dayOfYear(d) % TIPS.length]; },
    greeting: function (now) {
      var h = now.getHours();
      return h < 5 ? '夜深了' : h < 11 ? '早上好' : h < 14 ? '中午好' : h < 18 ? '下午好' : '晚上好';
    },
    coach: function (p) {
      var t = p.today(), streak = p.streak(), errors = p.errorCount(), acc = p.recentAccuracy();
      if (p.learned.size === 0 && p.answered === 0) return '第一次来？从元音「あいうえお」开始，今天认识 5 个假名就很棒。';
      if (t.sec / 60 >= p.goal) return '今日目标已完成！想多学可以继续，也可以把精力留给明天。';
      if (errors >= HEAVY_REVIEW) return '有 ' + errors + ' 个错题在等你，今天先专心复习，不急着学新内容。';
      if (acc >= 0 && acc < 0.7) return '最近正确率 ' + Math.round(acc * 100) + '%，建议放慢新内容，多翻卡、多听音。';
      if (streak >= 3) return '已经连续学习 ' + streak + ' 天，保持这个节奏！';
      if (streak === 0 && p.hasEarlierDays()) return '欢迎回来！先做个热身复习，再继续往前走。';
      return '按下面的建议逐项完成，大约需要 ' + p.goal + ' 分钟。';
    },
    nextGroup: function (p) {
      for (var i = 0; i < Groups.length; i++) {
        if (Groups[i].items.some(function (x) { return !p.learned.has(x.K); })) return Groups[i];
      }
      return null;
    },
    /** 今天的目标行：第一次生成计划时确定并记入当日记录，当天固定。 */
    todayGroup: function (p) {
      var t = p.today();
      if (t.group) { var g = groupById(t.group); if (g) return g; }
      var next = DailyPlan.nextGroup(p);
      if (!next || p.errorCount() >= HEAVY_REVIEW) return null;
      t.group = next.id;
      return next;
    },
    build: function (p) {
      var t = p.today(), learned = pool('learned', p), errors = p.errorCount();
      var target = DailyPlan.todayGroup(p), next = DailyPlan.nextGroup(p), tasks = [];
      if (errors > 0) {
        tasks.push({ title: '复习错题 · ' + errors + ' 个', detail: '先清旧账：错题在测验中连续答对两次即可移出。', button: '开始复习', action: 'reviewQuiz' });
      } else if (learned.length >= 5 && p.hasEarlierDays()) {
        tasks.push({ title: '热身：回顾已熟悉的假名', detail: '今天已翻 ' + Math.min(t.cards, 10) + ' / 10 张卡。', done: t.cards >= 10, button: '翻卡热身', action: 'warmup' });
      }
      if (target) {
        var known = target.items.filter(function (x) { return p.learned.has(x.K); }).length;
        var chars = target.items.map(function (x) { return x.K; }).join(' ');
        if (known >= target.items.length) {
          tasks.push({
            title: '学完新的一行：' + target.name, kana: chars,
            detail: '已熟悉 ' + known + ' / ' + known + (next ? '。还有余力可以继续学「' + next.name + '」。' : '。'),
            done: true, doneButton: next ? '学下一行' : '综合测验',
            action: next ? 'learnGroup' : 'allQuiz', scope: next ? next.id : null
          });
        } else {
          tasks.push({
            title: '学习新的一行：' + target.name, kana: chars,
            detail: '已熟悉 ' + known + ' / ' + target.items.length + '（翻卡记住或测验答对都算）',
            button: known > 0 ? '继续学习' : '开始学习', action: 'learnGroup', scope: target.id
          });
        }
      } else if (!next) {
        tasks.push({ title: '全部假名都已标记熟悉', detail: '用「全部假名」测验保持手感，下一步可以开始学片假名。', button: '综合测验', action: 'allQuiz' });
      } else if (errors >= HEAVY_REVIEW) {
        tasks.push({ title: '今天先不学新的一行', detail: '错题有 ' + errors + ' 个，先练熟它们，明天再学「' + next.name + '」效果更好。', action: null });
      }
      var quizScope;
      if (target && target.items.some(function (x) { return p.learned.has(x.K); })) quizScope = target.id;
      else if (learned.length >= 4) quizScope = 'learned';
      else quizScope = target ? target.id : (next ? next.id : 'all');
      tasks.push({
        title: '完成一轮 10 题测验',
        detail: t.answered > 0 ? '今天已答 ' + t.answered + ' 题，正确率 ' + Math.round(100 * t.correct / t.answered) + '%。' : '检查记忆最好的办法，就是试着回答。',
        done: t.answered >= 10, button: '去测验', action: 'quiz', scope: quizScope
      });
      var minutes = Math.floor(t.sec / 60);
      tasks.push({
        title: '专注学习 ' + p.goal + ' 分钟',
        detail: '今天已计时 ' + minutes + ' 分钟' + (minutes < p.goal ? '，还差 ' + (p.goal - minutes) + ' 分钟。' : '。'),
        done: minutes >= p.goal, button: '开始计时', doneButton: '再学一段', action: 'startTimer'
      });
      tasks.forEach(function (x) { x.done = !!x.done; if (!x.doneButton) x.doneButton = '再来一次'; });
      return tasks;
    },
    suggestedTimerMinutes: function (p) {
      var left = Math.ceil((p.goal * 60 - p.today().sec) / 60);
      return left <= 0 ? p.timerMinutes : Math.max(5, Math.min(60, left));
    },
    week: function (p) {
      var names = ['周日', '周一', '周二', '周三', '周四', '周五', '周六'], base = p.clock(), out = [];
      for (var i = 6; i >= 0; i--) {
        var d = addDays(base, -i), log = p.days[dayKey(d)];
        out.push({ date: d, label: i === 0 ? '今天' : names[d.getDay()], minutes: log ? Math.floor(log.sec / 60) : 0, studied: !!log && dayActive(log), isToday: i === 0 });
      }
      return out;
    }
  };

  var api = {
    VERSION: '2.4.0', THEMES: THEMES,
    Groups: Groups, All: All, ByKana: ByKana, groupById: groupById, pool: pool, deck: deck, note: note, audioKey: audioKey, shuffle: shuffle,
    GOAL_CHOICES: GOAL_CHOICES, TIMER_CHOICES: TIMER_CHOICES, dayKey: dayKey, addDays: addDays,
    Progress: Progress, Quiz: Quiz, StudyTimer: StudyTimer, DailyPlan: DailyPlan
  };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.KanaCore = api;
})(this);
