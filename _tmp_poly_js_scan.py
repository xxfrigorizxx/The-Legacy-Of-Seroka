import re
for p in ['c:/dev/Zero-K-Frozen-Legacy-main/_tmp_poly_pages_bundle.js','c:/dev/Zero-K-Frozen-Legacy-main/_tmp_poly_client.js']:
    s=open(p,'r',encoding='utf-8',errors='ignore').read()
    print('\nFILE',p)
    print('contains sWOZA id?', 'sWOZA820sH' in s)
    for kw in ['download','gltf','glb','fbx','api','poly.pizza','models','bundle']:
        print(kw, s.lower().count(kw))
    urls=sorted(set(re.findall(r'https://[^"\'\s)]+',s)))
    print('url_count',len(urls))
    for u in urls[:120]:
        if any(k in u.lower() for k in ['poly.pizza','download','gltf','glb','fbx','api']):
            print(' ',u)
