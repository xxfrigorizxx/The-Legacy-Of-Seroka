import re, html, json
p='c:/dev/Zero-K-Frozen-Legacy-main/_tmp_sketchfab_calf_page.html'
s=open(p,'r',encoding='utf-8',errors='ignore').read()
m=re.search(r'id="js-dom-data-prefetched-data"[^>]*><!--(.*?)--></div>', s)
if not m:
    print('prefetched not found')
    raise SystemExit
raw=html.unescape(m.group(1))
obj=json.loads(raw)
print('keys', len(obj))
for k in sorted(obj.keys()):
    if '7376659c08a24ffebf7d92523d6d499d' in k or 'download' in k or '/i/models/' in k:
        print('KEY',k)
        v=obj[k]
        txt=json.dumps(v)[:800]
        print(txt)
