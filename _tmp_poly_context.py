s=open('c:/dev/Zero-K-Frozen-Legacy-main/_tmp_poly_client.js','r',encoding='utf-8',errors='ignore').read()
for token in ['/api/model/','/api/v1.1','/download/','/api/search/']:
    print('\nTOKEN',token)
    start=0
    c=0
    while True:
        i=s.find(token,start)
        if i==-1 or c>=8:
            break
        print(s[max(0,i-160):i+220])
        print('---')
        start=i+len(token)
        c+=1
