import re
s = open('c:/dev/Zero-K-Frozen-Legacy-main/_tmp_poly_bundle.html','r',encoding='utf-8',errors='ignore').read()
patterns = [
    r'/api/[^"\'\s<]+',
    r'https://[^"\'\s<]*api[^"\'\s<]*',
    r'/download/[^"\'\s<]+',
    r'poly\.pizza/[^"\'\s<]*download[^"\'\s<]*',
]
for pat in patterns:
    matches = sorted(set(re.findall(pat, s)))
    print('\nPATTERN', pat, 'count', len(matches))
    for x in matches:
        print(x)
