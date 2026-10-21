// 安卓版核心逻辑自检（对应桌面版 SelfTest.cs）。运行：node tests/core.test.js
'use strict';
var path = require('path'), fs = require('fs');
var C = require(path.join(__dirname, '..', 'app', 'assets', 'www', 'core.js'));
var results = [];
function check(ok, name) { if (!ok) throw new Error('FAIL: ' + name); }
function section(name, fn) { fn(); results.push('PASS: ' + name); }
function at(y, m, d, h) { return function () { return new Date(y, m - 1, d, h || 9, 0, 0); }; }

section('104 kana; 46/25/33 group counts; sparse chart positions; card deck in chart order', function () {
  var p = new C.Progress();
  check(C.All.length === 104 && Object.keys(C.ByKana).length === 104, 'unique kana');
  check(C.pool('basic', p).length === 46 && C.pool('voiced', p).length === 25 && C.pool('combo', p).length === 33, 'groups');
  check(C.ByKana['を'].column === 4 && C.ByKana['や'].column === 0 && C.ByKana['ゆ'].column === 2 && C.ByKana['よ'].column === 4, 'chart spacing');
  check(C.deck('basic', p, false).slice(0, 12).map(function (x) { return x.K; }).join('') === 'あいうえおかきくけこさし', 'card deck follows chart order');
  check(C.deck('y', p, false).map(function (x) { return x.K; }).join('') === 'やゆよ' && C.deck('all', p, false).pop().K === 'ぴょ', 'card deck order for sparse rows and all');
  var sh = C.deck('basic', p, true).map(function (x) { return x.K; }).sort().join(''), pl = C.pool('basic', p).map(function (x) { return x.K; }).sort().join('');
  check(sh === pl, 'shuffled deck keeps the same cards');
});

section('300 randomized quizzes; distinct answers; score; repeat-submit protection', function () {
  var p = new C.Progress();
  for (var i = 0; i < 300; i++) {
    var q = new C.Quiz(i % 2 === 0 ? 'all' : 'nn', p);
    check(new Set(q.items.map(function (x) { return x.K; })).size === q.items.length, 'duplicate questions');
    while (!q.done()) {
      check(q.options.length === 4 && new Set(q.options).size === 4 && q.options.indexOf(q.current().R) >= 0, 'choices');
      check(q.answer(q.current().R), 'answer');
      var n = p.answered;
      check(!q.answer(q.current().R) && p.answered === n, 'duplicate submit');
      q.next();
    }
    check(q.score === q.items.length, 'score');
  }
});

section('wrong-answer review; two correct answers; reset on mistake; JSON persistence; empty review', function () {
  var p = new C.Progress(), k = C.ByKana['ん'];
  p.learned.add(k.K);
  p.record(k, false);
  check(p.errors[k.K] === 2 && !p.learned.has(k.K), 'wrong adds review');
  p.record(k, true); check(p.errors[k.K] === 1, 'first review');
  p.record(k, false); check(p.errors[k.K] === 2, 'review resets');
  p.record(k, true); p.record(k, true); check(!p.errors[k.K], 'review clears');
  p.learned.add('あ'); p.markAgain(C.ByKana['きゃ']);
  var q = C.Progress.fromJSON(JSON.parse(JSON.stringify(p.toJSON())));
  check(q.learned.has('あ') && q.errors['きゃ'] === 2 && q.answered === p.answered && q.correct === p.correct, 'reload');
  var bad = C.Progress.fromJSON({ learned: ['あ', 'X'], errors: { 'い': 9, 'Q': 1 }, goal: 7, answered: 3, correct: 99, days: { 'bad': {}, '2026-10-19': { sec: -5, group: 'zz' } } });
  check(bad.learned.size === 1 && bad.errors['い'] === 2 && !bad.errors.Q && bad.goal === 15 && bad.correct === 3, 'sanitize');
  check(bad.days['2026-10-19'].sec === 0 && bad.days['2026-10-19'].group === null && !bad.days.bad, 'sanitize days');
  check(C.Progress.fromJSON(null).learned.size === 0 && C.Progress.fromJSON('garbage').answered === 0, 'garbage input');
  check(new C.Quiz('errors', new C.Progress()).items.length === 0, 'empty review');
});

section('daily log; streak; plan ordering; heavy-review pause; goal/timer/theme persistence; week data', function () {
  var now = at(2026, 10, 19), p = new C.Progress();
  p.clock = now;
  check(p.streak() === 0 && !p.hasEarlierDays(), 'empty streak');
  var fresh = C.DailyPlan.build(p);
  check(fresh.some(function (t) { return t.action === 'learnGroup' && t.scope === 'a'; }), 'first plan starts with vowels');
  check(!fresh.some(function (t) { return t.action === 'reviewQuiz' || t.action === 'warmup'; }), 'no review on first day');
  check(C.DailyPlan.coach(p).indexOf('第一次') >= 0, 'first-time coach');
  for (var i = 1; i <= 3; i++) p.day(C.addDays(now(), -i)).sec = 600;
  check(p.streak() === 3 && p.hasEarlierDays(), 'streak counts from yesterday');
  p.addStudySeconds(45); check(p.streak() === 4, 'streak includes today');
  p.day(C.addDays(now(), -2)).sec = 0; check(p.streak() === 2, 'gap breaks streak');
  C.Groups[0].items.forEach(function (k) { p.cardKnown(k); });
  check(p.today().learned === 5 && p.today().cards === 5, 'card counters');
  p.cardKnown(C.Groups[0].items[0]); check(p.today().learned === 5 && p.today().cards === 6, 'relearn not double counted');
  check(C.DailyPlan.nextGroup(p).id === 'k', 'next group');
  p.record(C.ByKana['か'], false); p.record(C.ByKana['あ'], true);
  check(p.today().answered === 2 && p.today().correct === 1, 'daily quiz counters');
  check(C.DailyPlan.build(p)[0].action === 'reviewQuiz', 'errors reviewed first');
  C.All.slice(0, C.DailyPlan.HEAVY_REVIEW).forEach(function (k) { p.markAgain(k); });
  check(C.DailyPlan.build(p).some(function (t) { return t.action === 'learnGroup'; }), "today's row kept");
  p.today().group = null;
  check(C.DailyPlan.build(p).some(function (t) { return t.action === null; }) && p.today().group === null, 'heavy review pauses');
  p.goal = 20; p.timerMinutes = 25;
  var q = C.Progress.fromJSON(JSON.parse(JSON.stringify(p.toJSON()))); q.clock = now;
  check(q.goal === 20 && q.timerMinutes === 25, 'goal reload');
  check(q.theme === 'system', 'theme default');
  p.theme = 'dark';
  check(C.Progress.fromJSON(JSON.parse(JSON.stringify(p.toJSON()))).theme === 'dark', 'theme reload');
  check(C.Progress.fromJSON({ theme: 'neon' }).theme === 'system', 'theme sanitize');
  check(Math.abs(q.today().sec - 45) < 1 && q.today().learned === 5 && q.today().answered === 2, 'day log reload');
  check(q.streak() === 2, 'streak reload');
  var w = C.DailyPlan.week(q);
  check(w.length === 7 && w[6].isToday && w[5].minutes === 10, 'week data');
  check(C.DailyPlan.suggestedTimerMinutes(q) === 20, 'suggested timer');
});

section('row completion via quiz; fixed daily row; review gating; detail toggle; next-day advance', function () {
  var p = new C.Progress(); p.clock = at(2026, 10, 19, 14);
  function learnTask() { return C.DailyPlan.build(p).filter(function (t) { return t.title.indexOf('一行') >= 0; })[0]; }
  var b = learnTask();
  check(!b.done && b.scope === 'a' && p.today().group === 'a', 'today group assigned');
  for (var r = 0; r < 3; r++) C.Groups[0].items.forEach(function (k) { p.record(k, true); });
  check(p.today().answered === 15 && p.today().learned === 5, 'quiz-only learning counted');
  var a = learnTask();
  check(a.done && a.title.indexOf('元音') >= 0 && a.scope === 'k', 'row done via quiz');
  var q = C.Progress.fromJSON(JSON.parse(JSON.stringify(p.toJSON()))); q.clock = p.clock;
  check(q.today().group === 'a', 'group reload');
  p.record(C.ByKana['あ'], false); p.record(C.ByKana['あ'], true);
  check(!p.learned.has('あ') && !learnTask().done, 'pending review not learned');
  p.record(C.ByKana['あ'], true); check(p.learned.has('あ') && learnTask().done, 'review cleared relearns');
  var n = p.today().learned;
  p.toggleLearned(C.ByKana['か']); check(p.learned.has('か') && p.today().learned === n + 1, 'toggle on');
  p.toggleLearned(C.ByKana['か']); check(!p.learned.has('か') && p.today().learned === n, 'toggle off');
  p.clock = at(2026, 10, 20, 9);
  var nx = learnTask(); check(!nx.done && nx.scope === 'k' && p.today().group === 'k', 'next day moves on');
});

section('timer tick; pause; add time; sleep clamp; finish; restart; reset; limits; restore', function () {
  var t0 = Date.UTC(2026, 0, 1), T = C.StudyTimer, t = new T(1), r;
  check(t.total === 60 && !t.started() && T.format(t.remaining) === '01:00', 'initial');
  t.start(t0); r = t.tick(t0 + 10000);
  check(Math.abs(r.credit - 10) < 0.01 && !r.finished && T.format(t.remaining) === '00:50', 'tick');
  check(!t.set(5), 'no preset while running');
  var c = t.pause(t0 + 20000); check(Math.abs(c - 10) < 0.01 && !t.running && t.started(), 'pause');
  r = t.tick(t0 + 500000); check(r.credit === 0 && Math.abs(t.remaining - 40) < 0.01, 'paused frozen');
  t.start(t0 + 500000); t.addSeconds(300);
  check(t.total === 360 && Math.abs(t.remaining - 340) < 0.01, 'add five minutes');
  r = t.tick(t0 + 500000 + 3600000);
  check(r.finished && r.credit <= T.MAX_CREDIT && t.finished() && t.remaining === 0, 'sleep clamp and finish');
  t.start(t0 + 5000000); check(t.running && Math.abs(t.remaining - 360) < 0.01, 'restart');
  t.reset(); check(!t.running && !t.started(), 'reset');
  check(t.set(120) && t.total === T.MAX_SECONDS, 'clamp');
  var s = new T(15); s.start(t0);
  var back = T.restore(s.snapshot(), 15, t0 + 60000);
  check(back.running && Math.abs(back.remaining - 840) < 0.01 && back.tick(t0 + 61000).credit <= 1.01, 'restore running');
  var gone = T.restore(s.snapshot(), 15, t0 + 3600000);
  check(!gone.running && gone.finished(), 'restore expired');
});

section('all 104 recordings bundled and mapped', function () {
  var keys = C.All.map(C.audioKey);
  check(new Set(keys).size === 104, 'audio mapping');
  var dir = path.join(__dirname, '..', 'app', 'assets', 'www', 'audio');
  keys.forEach(function (k) {
    var f = path.join(dir, k + '.mp3');
    check(fs.existsSync(f) && fs.statSync(f).size > 1000, 'audio file ' + k);
  });
});

section('stroke data for all 104 kana; textbook stroke counts (2.3.0)', function () {
  var vm = require('vm'), ctx = { window: {} };
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, '..', 'app', 'assets', 'www', 'data', 'kana-strokes.js'), 'utf8'), ctx);
  var S = ctx.window.KANA_STROKES;
  function count(k) { return k.split('').reduce(function (n, c) { return n + (S[c] ? S[c].length : NaN); }, 0); }
  C.All.forEach(function (k) {
    k.K.split('').forEach(function (c) { check(S[c] && S[c].length > 0 && S[c].every(function (d) { return /^M[-\d.,c\s]+$/.test(d); }), 'strokes ' + c); });
  });
  var expected = { 'あ': 3, 'い': 2, 'き': 4, 'さ': 3, 'ふ': 4, 'を': 3, 'ん': 1, 'ぱ': 4, 'きゃ': 7, 'びょ': 5 };
  Object.keys(expected).forEach(function (k) { check(count(k) === expected[k], 'stroke count ' + k); });
  var font = path.join(__dirname, '..', 'app', 'assets', 'www', 'fonts', 'KleeOne-SemiBold-kana.ttf');
  check(fs.existsSync(font) && fs.statSync(font).size > 30000, 'kana font bundled');
});

console.log(results.join('\n'));
console.log('全部 ' + results.length + ' 组通过');
