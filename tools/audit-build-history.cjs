// Read-only audit: emits only EEV build evidence, never copies conversation text.
const fs = require('node:fs');
const path = require('node:path');
const cp = require('node:child_process');
const root = process.argv[2];
const cutoff = '2026-11-03T09:00:00.000Z'; // before the build-number implementation
const files = cp.execFileSync('rg', ['-l', '--fixed-strings', 'EliteEnemyVariants', root, '-g', '*.jsonl'], {encoding:'utf8'}).trim().split(/\r?\n/);
const calls = new Map(), outputs = new Map();
(async () => {
for (const file of files) {
  const lines = require('node:readline').createInterface({input:fs.createReadStream(file),crlfDelay:Infinity});
  for await (const line of lines) {
    if (!line.includes('response_item')) continue;
    let r; try { r = JSON.parse(line); } catch { continue; }
    if (r.timestamp > cutoff || r.type !== 'response_item') continue;
    const p = r.payload;
    if (p.type.endsWith('call_output')) {
      if (!calls.has(p.call_id)) continue;
      const value = typeof p.output === 'string' ? p.output : JSON.stringify(p.output);
      outputs.set(p.call_id, (outputs.get(p.call_id)||'') + value);
    } else if (['function_call','custom_tool_call'].includes(p.type) && !calls.has(p.call_id)) {
      if (!/EliteEnemyVariants/i.test(p.input||p.arguments) || !/dotnet.{0,15}(build|msbuild)|Build-Package/.test(p.input||p.arguments)) continue;
      calls.set(p.call_id, {time:r.timestamp, name:p.name, code:p.input||p.arguments, source:path.basename(file)});
    }
  }
}
const builds = [];
for (const [id,c] of calls) {
  if (!/exec|shell/.test(c.name)) continue;
  const commands = [];
  if (c.name === 'exec' || c.name === 'functions.exec') {
    // Extract literal commands from executed shell calls, not patches, comments or read_thread results.
    for (const m of c.code.matchAll(/tools\.(?:exec_command|shell_command)\s*\(\s*\{\s*(?:"?(?:cmd|command)"?)\s*:\s*("(?:\\.|[^"\\])*")/g)) {
      commands.push(JSON.parse(m[1]));
    }
  } else {
    try { const a=JSON.parse(c.code); if(a.cmd||a.command) commands.push(a.cmd||a.command); } catch {}
  }
  for (const command of commands) {
    // Explicit EEV builds, or package-script invocations inside the EEV directory.
    for (const m of command.matchAll(/(?:^|[;\n])\s*(dotnet\s+(?:build|msbuild)\s+[^;\n]*|&?\s*['"]?[^;\n]*Build-Package\.ps1['"]?[^;\n]*)/g)) {
      const build=m[1];
      if (/^(?:Get-Content|git|rg|Select-|Write-)/i.test(build) || !/EliteEnemyVariants/i.test(command+c.code)) continue;
      if (/dotnet/.test(build) && !/EliteEnemyVariants(?:[\\/]EliteEnemyVariants)?\.csproj/.test(build)) continue;
      if (/Build-Package/.test(build) && !/^&?\s*['"]?[^\s;]*Build-Package\.ps1/.test(build)) continue;
      const out=outputs.get(id)||'';
      const marker=out.match(/ビルドに成功しました|Build succeeded|ビルドに失敗しました|Build FAILED/i);
      const outcome=marker ? (/成功|succeeded/i.test(marker[0])?'succeeded':'failed') : /error CS\d+|error MSB\d+|error NU\d+/.test(out)?'failed':'unknown';
      builds.push({timestamp:c.time,callId:id,command:build, outcome, source:c.source});
    }
  }
}
builds.sort((a,b)=>a.timestamp.localeCompare(b.timestamp));
console.log(JSON.stringify({cutoff,scannedFiles:files.length,entries:builds.map((r,i)=>({number:i+1,...r}))},null,2));
})();
