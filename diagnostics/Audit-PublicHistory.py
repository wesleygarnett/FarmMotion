"""Targeted credential/privacy screening; never prints matched secret values."""
import subprocess,re,json,sys
from pathlib import Path
repo=sys.argv[1]
def git(*args): return subprocess.check_output(['git','-C',repo,*args])
patterns={
 'github_token':rb'(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})',
 'private_key':rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
 'aws_access_key':rb'AKIA[0-9A-Z]{16}',
 'openai_key':rb'sk-(?:proj-)?[A-Za-z0-9_-]{40,}',
 'assigned_secret':rb'(?i)(?:password|api_key|client_secret|access_token)\s*[:=]\s*["\'][^"\'\r\n]{16,}["\']',
 'personal_windows_path':rb'(?i)[A-Z]:[/\\]Users[/\\](?!Public|Default)[A-Za-z0-9_. -]+'
}
findings=[];count=0
for row in git('rev-list','--objects','--all').splitlines():
 parts=row.split(b' ',1)
 if len(parts)!=2: continue
 oid,name=parts
 if git('cat-file','-t',oid.decode()).strip()!=b'blob': continue
 data=git('cat-file','blob',oid.decode())
 if b'\0' in data[:4096]: continue
 count+=1
 for kind,pat in patterns.items():
  if re.search(pat,data): findings.append({'kind':kind,'object':oid.decode()[:12],'path':name.decode(errors='replace')})
if len(sys.argv)>2:
 for file in Path(sys.argv[2]).glob('*.txt'):
  data=file.read_bytes()
  for kind,pat in patterns.items():
   if re.search(pat,data): findings.append({'kind':kind,'path':file.name})
print(json.dumps({'text_blobs':count,'findings':findings},indent=2))
