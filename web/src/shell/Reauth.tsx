// A server-required recent proof. Never stores proof or replays the action that was refused. The server alone decides
// whether the proof is recent enough; its timestamp is informational, not a client-side authorization decision.
import { useState, type FormEvent } from 'react';
import { ep, useSession } from '../app/context';
import { isApiError } from '../api/http';
import { Banner, Button, Modal, TextField, errorText } from '../ui/kit';

export function Reauth() {
  const me = useSession(s => s.me);
  const dismiss = useSession(s => s.dismissReauth);
  const [method, setMethod] = useState<'password' | 'code'>('password');
  const [proof, setProof] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);
  const submit = async (e: FormEvent) => {
    e.preventDefault(); setBusy(true); setError(null);
    try {
      await ep.reauth(method === 'password' ? { password: proof } : { code: proof.replace(/\s/g, '') });
      setProof(''); setDone(true);
    } catch (x) {
      setError(isApiError(x) ? x.message + (x.retryAfter ? ` Try again in ${x.retryAfter} s.` : '') : errorText(x));
      setProof('');
    } finally { setBusy(false); }
  };
  return <Modal title="Confirm your identity" onClose={dismiss}>
    {done ? <div className="wa-form">
      <Banner tone="ok" title="Identity confirmed">The original action was not run. Close this dialog and explicitly try it again.</Banner>
      <Button kind="primary" onClick={dismiss}>Return to the action</Button>
    </div> : <form className="wa-form" onSubmit={submit}>
      <p>This action needs a recent password or enrolled authenticator code. Nothing runs automatically after confirmation.</p>
      {me?.twoFactor && <label className="k-field">Verify with
        <select className="k-select" value={method} onChange={e => { setMethod(e.target.value as 'password' | 'code'); setProof(''); setError(null); }}>
          <option value="password">Password</option><option value="code">Authenticator code</option>
        </select>
      </label>}
      <TextField label={method === 'password' ? 'Your password' : 'Authenticator code'} type={method === 'password' ? 'password' : 'text'} value={proof} onChange={setProof}
        autoComplete={method === 'password' ? 'current-password' : 'one-time-code'} inputMode={method === 'code' ? 'numeric' : 'text'} autoFocus />
      {error && <Banner tone="err" title={error} />}
      <div className="wa-row"><Button kind="primary" type="submit" busy={busy} disabled={!proof}>Confirm identity</Button><Button kind="ghost" onClick={dismiss}>Cancel</Button></div>
    </form>}
  </Modal>;
}
