import re
p='c:/dev/Zero-K-Frozen-Legacy-main/_tmp_poly_bundle.html'
s=open(p,'r',encoding='utf-8',errors='ignore').read()
urls=set(re.findall(r'https://[^"\'\s<>]+',s))
for u in sorted(urls):
    if any(k in u.lower() for k in ['download','gltf','glb','fbx','poly.pizza/api','api.poly.pizza','bundle','quaternius']):
        print(u)
print('total_urls',len(urls))
