// Webhooks: opt-in Discord and JSON hooks. URLs are secrets: the panel never shows them again after saving (only host
// and Discord webhook id); changes and tests ask for recent proof. The event picker groups topics by their prefix, and
// the template help lists the chosen events' fields.
import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useSession } from "../app/context";
import { isApiError } from "../api/http";
import { groupEvents, type EventKind, type Webhook, type WebhookInput } from "../api/laneC";
import { lc } from "../ext/client";
import { can } from "../state/session";
import { Banner, Button, Card, Check, Empty, LoadError, Loading, Modal, Page, Pill, SelectField, TextAreaField, TextField, Toggle, explain, when } from "../ui/kit";
import "../laneC.css";

const why = (e: unknown) => (isApiError(e) && e.code === "reauth_required" ? "Confirm it is you in the dialog, then try again. Nothing was changed." : explain(e));

export function WebhooksPage() {
  const me = useSession((s) => s.me);
  const edit = can(me, "webhooks.edit");
  const hooks = useQuery({ queryKey: ["webhooks"], queryFn: lc.webhooks });
  const events = useQuery({ queryKey: ["webhook-events"], queryFn: lc.webhookEvents });
  const log = useQuery({ queryKey: ["webhook-deliveries"], queryFn: lc.deliveries, refetchInterval: 5000 });
  const settings = useQuery({ queryKey: ["webhook-settings"], queryFn: lc.webhookSettings });
  const privateOn = !!settings.data?.allowPrivateTargets;
  const [open, setOpen] = useState<Webhook | "new" | null>(null);
  const [msg, setMsg] = useState<string | null>(null);
  const act = async (f: () => Promise<unknown>, done: string) => {
    try {
      await f();
      setMsg(done);
      void hooks.refetch();
      void log.refetch();
      void settings.refetch();
    } catch (e) {
      setMsg(why(e));
    }
  };
  return (
    <Page title="Webhooks" wide sub="Send chosen events to Discord or any HTTPS endpoint. Nothing is sent until you add a webhook."
      actions={edit && <Button kind="primary" onClick={() => setOpen("new")}>Add webhook</Button>}>
      <div aria-live="polite">{msg && <Banner tone="info" title={msg} actions={<Button size="s" kind="ghost" onClick={() => setMsg(null)}>Close</Button>} />}</div>
      {hooks.isError && <LoadError what="The webhooks" error={hooks.error} retry={() => void hooks.refetch()} />}
      {privateOn && (
        <Banner tone="warn" title="Private network targets are ON" actions={settings.data?.canChange &&
          <Button size="s" kind="primary" onClick={() => void act(() => lc.setPrivateTargets(false), "Private targets are off: public internet addresses only.")}>Turn off</Button>}>
          <span data-testid="wa-private-targets-warning">{settings.data?.warning}</span>
        </Banner>
      )}
      {settings.data?.canChange && !privateOn && (
        <Card title="Private network targets" sub="Off: webhooks only reach public internet addresses. This is the safe setting.">
          <p className="k-muted">Turning this on lets webhooks reach private LAN addresses (10/8, 172.16/12, 192.168/16, fc00::/7), over plain http:// too.
            Anyone who can add a webhook could then make this server send requests into your network. Loopback, link-local and cloud metadata stay blocked.</p>
          <Button kind="danger" onClick={() => window.confirm("Let webhooks reach private LAN addresses? This lowers protection.") &&
            void act(() => lc.setPrivateTargets(true), "Private targets are on.")}>Allow private network targets</Button>
        </Card>
      )}
      <Card title="Webhooks">
        {hooks.isLoading && <Loading />}
        {hooks.data && !hooks.data.hooks.length && <Empty title="No webhooks yet">
          Send joins, deaths, restarts, crashes, failed saves and more to a Discord channel or any HTTPS endpoint.</Empty>}
        {!!hooks.data?.hooks.length && (
          <div className="wa-table-wrap"><table className="wa-table">
            <thead><tr><th>Name</th><th>Kind</th><th>Where</th><th>Events</th><th>State</th><th /></tr></thead>
            <tbody>{hooks.data.hooks.map((h) => (
              <tr key={h.id}>
                <td data-label="Name">{h.name}</td><td data-label="Kind">{h.kind}</td><td data-label="Where" className="k-mono wa-break">{h.url}</td>
                <td data-label="Events" className="wa-break">{h.events.map((t) => titleOf(events.data?.events, t)).join(", ") || <span className="k-muted">none</span>}</td>
                <td data-label="State">{h.enabled ? <Pill tone="ok">on</Pill> : <Pill>off</Pill>}</td>
                <td className="wa-actions">{edit && <>
                  <Button size="s" aria-label={`Test ${h.name}`} onClick={() => void act(() => lc.testWebhook(h.id), "Test queued: see the deliveries below.")}>Test</Button>
                  <Button size="s" kind="ghost" aria-label={`Edit ${h.name}`} onClick={() => setOpen(h)}>Edit</Button>
                  <Button size="s" kind="danger" aria-label={`Remove ${h.name}`} onClick={() => window.confirm(`Remove "${h.name}"?`) && void act(() => lc.deleteWebhook(h.id), "Removed.")}>Remove</Button>
                </>}</td>
              </tr>))}</tbody>
          </table></div>
        )}
      </Card>
      <Card title="Recent deliveries" sub="The last 100 sends: no URLs, no bodies.">
        {log.isError && <LoadError what="The deliveries" error={log.error} retry={() => void log.refetch()} />}
        {log.data && !log.data.deliveries.length && <Empty title="Nothing sent yet">Each send shows here with its result.</Empty>}
        {!!log.data?.deliveries.length && (
          <div className="wa-table-wrap"><table className="wa-table wa-table-compact">
            <thead><tr><th>When</th><th>Webhook</th><th>Event</th><th>Result</th></tr></thead>
            <tbody>{log.data.deliveries.map((d, i) => (
              <tr key={i}><td data-label="When" className="wa-nowrap">{when(d.at)}</td><td data-label="Webhook">{d.name}</td><td data-label="Event">{titleOf(events.data?.events, d.event)}</td>
                <td data-label="Result">{d.ok ? <Pill tone="ok">sent</Pill> : <Pill tone="err">{d.error ?? "failed"}</Pill>}</td></tr>))}</tbody>
          </table></div>
        )}
      </Card>
      {open && <HookDialog hook={open === "new" ? null : open} topics={events.data?.events ?? []} privateOn={privateOn} onClose={() => setOpen(null)}
        onSave={async (input) => {
          await (open === "new" ? lc.addWebhook(input) : lc.updateWebhook(open.id, input));
          setMsg(`Saved ${input.name}.`);
          setOpen(null);
          void hooks.refetch();
        }} />}
    </Page>
  );
}

function titleOf(events: EventKind[] | undefined, topic: string): string {
  return events?.find((e) => e.topic === topic)?.title ?? topic;
}

function HookDialog({ hook, topics, privateOn, onClose, onSave }: { hook: Webhook | null; topics: EventKind[]; privateOn: boolean; onClose: () => void; onSave: (h: WebhookInput) => Promise<void> }) {
  const [name, setName] = useState(hook?.name ?? "");
  const [kind, setKind] = useState<"discord" | "json">(hook?.kind ?? "discord");
  const [url, setUrl] = useState("");
  const [events, setEvents] = useState<string[]>(hook?.events ?? []);
  const [enabled, setEnabled] = useState(hook?.enabled ?? true);
  const [template, setTemplate] = useState(hook?.template ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const groups = useMemo(() => groupEvents(topics), [topics]);
  const toggle = (t: string) => setEvents((e) => (e.includes(t) ? e.filter((x) => x !== t) : [...e, t]));
  // the chosen events' fields, each with the events that carry it
  const fields = useMemo(() => {
    const m = new Map<string, string[]>();
    for (const t of topics.filter((x) => events.includes(x.topic))) for (const f of t.fields) m.set(f, [...(m.get(f) ?? []), t.topic]);
    return [...m.entries()];
  }, [topics, events]);
  const ok = name.trim() && (hook || url.trim()) && events.length > 0;
  return (
    <Modal title={hook ? `Edit ${hook.name}` : "Add a webhook"} onClose={onClose} wide footer={<>
      <Button kind="ghost" onClick={onClose}>Cancel</Button>
      <Button kind="primary" busy={busy} disabled={!ok} onClick={async () => {
        setBusy(true);
        setError(null);
        try {
          await onSave({ name: name.trim(), kind, url: url.trim() || undefined, events, enabled, template: template.trim() || null });
        } catch (e) {
          setError(why(e));
        } finally {
          setBusy(false);
        }
      }}>Save</Button>
    </>}>
      <div className="wa-form wa-form-wide">
        <div className="wa-form-grid">
          <TextField label="Name" value={name} onChange={setName} maxLength={64} />
          <SelectField label="Kind" value={kind} onChange={setKind} options={[{ value: "discord", label: "Discord" }, { value: "json", label: "JSON (any HTTPS endpoint)" }]} />
        </div>
        <TextField label={hook ? "New URL (optional)" : "URL"} value={url} onChange={setUrl} type="password" autoComplete="off" mono
          hint={hook ? `Now ${hook.url}. Leave empty to keep it.` : privateOn
            ? "https, or http:// for a JSON hook to a private LAN address (private targets are on). Never loopback. It is not shown again after saving."
            : kind === "discord" ? "The channel's webhook URL (Discord: channel settings, Integrations, Webhooks). https only. It is not shown again after saving."
            : "https only, public internet addresses only. It is not shown again after saving."} />
        <fieldset className="wa-fieldset">
          <legend>Events {events.length > 0 && <span className="k-muted">({events.length} chosen)</span>}</legend>
          {!topics.length && <p className="k-muted wa-p">The event list did not load. Close this and try again.</p>}
          {groups.map((g) => (
            <div key={g.group} role="group" aria-label={g.group}>
              <h3 className="wa-group-title">{g.group}</h3>
              <div className="wa-checks">
                {g.events.map((t) => <Check key={t.topic} label={t.title} hint={t.topic} checked={events.includes(t.topic)} onChange={() => toggle(t.topic)} />)}
              </div>
            </div>
          ))}
        </fieldset>
        <TextAreaField label="Message template (optional)" value={template} onChange={setTemplate} maxLength={500} rows={3}
          hint={<>
            Empty: the panel's own message for each event. In a template, these are replaced:
            <ul className="wa-template-help">
              <li><code>{"{{event}}"}</code>: the event's name, such as player.join</li>
              <li><code>{"{{time}}"}</code>: when it happened (UTC)</li>
              {fields.map(([f, ts]) => <li key={f}><code>{`{{${f}}}`}</code>: from {ts.map((t) => titleOf(topics, t).toLowerCase()).join(", ")}</li>)}
              {!fields.length && <li>and the fields of the events you choose (pick events to see them)</li>}
            </ul>
          </>} />
        <Toggle label="On" checked={enabled} onChange={setEnabled} hint="Off: nothing is sent, the settings are kept." />
        <div aria-live="polite">{error && <Banner tone={error.startsWith("Confirm") ? "warn" : "err"} title={error} />}</div>
      </div>
    </Modal>
  );
}
