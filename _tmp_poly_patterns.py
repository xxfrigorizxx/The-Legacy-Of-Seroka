import re
s=open('c:/dev/Zero-K-Frozen-Legacy-main/_tmp_poly_client.js','r',encoding='utf-8',errors='ignore').read()
patterns=[r'/api/[^"\'\s)]+',r'/v1/[^"\'\s)]+',r'static\.poly\.pizza/[^"\'\s)]+',r'poly\.pizza/[^"\'\s)]+',r'/download[^"\'\s)]*']
for pat in patterns:
    m=sorted(set(re.findall(pat,s)))
    print('\nPATTERN',pat,'count',len(m))
    for x in m[:120]:
        print(x)
