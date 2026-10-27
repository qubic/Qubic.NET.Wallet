import importlib.util, os, sys, json
spec=importlib.util.spec_from_file_location("strings","strings.py"); m=importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
S=dict(m.STRINGS)
add=json.load(sys.stdin)
for k,v in add.items(): S[k]=v
with open("strings.py","w",encoding="utf-8") as o:
    o.write("# Master translation table. Keys prefixed by area. en is the source.\nSTRINGS = {\n")
    for k,v in S.items():
        parts=", ".join(f'"{c}":{repr(v[c])}' for c in ["en","es","fr","de","ru","zh"] if c in v)
        o.write(f'  {repr(k)}: {{{parts}}},\n')
    o.write("}\n")
print("merged; total", len(S))
