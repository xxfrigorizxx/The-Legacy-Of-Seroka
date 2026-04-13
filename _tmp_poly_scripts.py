import re
s = open('c:/dev/Zero-K-Frozen-Legacy-main/_tmp_poly_bundle.html','r',encoding='utf-8',errors='ignore').read()
for src in re.findall(r'<script[^>]+src="([^"]+)"', s):
    print(src)
