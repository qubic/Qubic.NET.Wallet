#!/usr/bin/env python3
# Master UI string table for the Qubic.Net wallet. Edit STRINGS, run to regenerate all 6 resx.
import os, xml.sax.saxutils as sx, importlib.util, sys
BASE = "/home/claude/Qubic.Net.Wallet/Resources/Localization"
LANGS = ["es","fr","de","ru","zh"]  # en is the neutral base file
def load_strings():
    # strings.py holds STRINGS = { key: {en,es,fr,de,ru,zh} }
    spec = importlib.util.spec_from_file_location("strings", os.path.join(os.path.dirname(__file__), "strings.py"))
    m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m); return m.STRINGS
HEADER='''<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
'''
def write(strings, culture, fname):
    rows=[]
    for k,v in strings.items():
        if culture not in v and culture!="en":
            print(f"MISSING {culture} for {k}", file=sys.stderr)
        text=v.get(culture, v["en"])
        rows.append(f'  <data name="{k}" xml:space="preserve">\n    <value>{sx.escape(text)}</value>\n  </data>')
    open(fname,"w",encoding="utf-8").write(HEADER+"\n".join(rows)+"\n</root>\n")
def main():
    strings=load_strings()
    # validate every key has all langs
    bad=0
    for k,v in strings.items():
        for c in ["en"]+LANGS:
            if c not in v: print(f"KEY {k} missing {c}", file=sys.stderr); bad+=1
    write(strings,"en",f"{BASE}/AppStrings.resx")
    for c in LANGS: write(strings,c,f"{BASE}/AppStrings.{c}.resx")
    print(f"{len(strings)} keys x 6 langs; {bad} gaps")
if __name__=="__main__": main()
