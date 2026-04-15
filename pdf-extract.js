const fs = require('fs');
const zlib = require('zlib');

function ascii85Decode(input) {
  let s = input.replace(/\s+/g, '');
  s = s.replace(/^<~/, '').replace(/~>$/, '');
  const out = [];
  let group = [];
  for (let i = 0; i < s.length; i++) {
    const ch = s[i];
    if (ch === 'z' && group.length === 0) { out.push(0,0,0,0); continue; }
    const code = ch.charCodeAt(0);
    if (code < 33 || code > 117) continue;
    group.push(code - 33);
    if (group.length === 5) {
      let acc = 0;
      for (let j = 0; j < 5; j++) acc = acc * 85 + group[j];
      out.push((acc >>> 24) & 255, (acc >>> 16) & 255, (acc >>> 8) & 255, acc & 255);
      group = [];
    }
  }
  if (group.length > 0) {
    const originalLength = group.length;
    while (group.length < 5) group.push(84);
    let acc = 0;
    for (let j = 0; j < 5; j++) acc = acc * 85 + group[j];
    const bytes = [(acc >>> 24) & 255, (acc >>> 16) & 255, (acc >>> 8) & 255, acc & 255];
    for (let i = 0; i < originalLength - 1; i++) out.push(bytes[i]);
  }
  return Buffer.from(out);
}

function extractTextFromPdf(file) {
  const content = fs.readFileSync(file).toString('latin1');
  const streams = [...content.matchAll(/stream([\s\S]*?)endstream/g)].map(x => x[1]);
  const decoded = [];
  for (const s of streams) {
    try {
      const a = ascii85Decode(s);
      let d;
      try { d = zlib.inflateSync(a); } catch { d = zlib.inflateRawSync(a); }
      decoded.push(d.toString('latin1'));
    } catch {}
  }
  const out = [];
  for (const stream of decoded) {
    const blocks = stream.match(/BT[\s\S]*?ET/g) || [];
    for (const b of blocks) {
      const m = b.match(/\((?:\\.|[^\\)])*\)\s*Tj|\[(.*?)\]\s*TJ/g) || [];
      for (const chunk of m) {
        if (chunk.includes(' Tj')) {
          const x = chunk.match(/\(((?:\\.|[^\\)])*)\)\s*Tj/);
          if (x) out.push(x[1]);
        } else {
          const y = chunk.match(/\((?:\\.|[^\\)])*\)/g) || [];
          for (const k of y) out.push(k.slice(1, -1));
        }
      }
    }
  }
  return out.join('\n')
    .replace(/\\\(/g, '(')
    .replace(/\\\)/g, ')')
    .replace(/\\n/g, '\n')
    .replace(/\\r/g, '\r')
    .replace(/\\t/g, '\t')
    .replace(/\\222/g, "'")
    .replace(/\\340/g, 'a')
    .replace(/\\351/g, 'e')
    .replace(/\\352/g, 'e')
    .replace(/\\364/g, 'o');
}

const file = process.argv[2];
console.log(extractTextFromPdf(file));
