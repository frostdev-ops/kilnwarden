// Player activity (DESIGN §5: join and leave history): who is on now and for how long, the newest joins, leaves and
// deaths (filtered by kind and player, "Older" pages back with `before`), and playtime per player over 7 and 30 days.
// "This is you" marks the player whose SteamID the signed-in account linked (My account > Linked sign-ins).
import { useInfiniteQuery, useQuery, type UseQueryResult } from "@tanstack/react-query";
import { LogIn, LogOut, Skull } from "lucide-react";
import { useMemo, useState } from "react";
import { panel } from "../app/panel";
import { eventText, type ActivityEvent, type ActivityOnline, type ActivityType, type OnlinePlayer } from "../api/panel";
import { ago, duration } from "../ui/format";
import { Banner, Button, Card, Empty, LoadError, Loading, Page, Pill, SelectField, when } from "../ui/kit";

const PAGE = 50;
const TYPES: { value: ActivityType | ""; label: string }[] = [
  { value: "", label: "Everything" }, { value: "join", label: "Joins" }, { value: "leave", label: "Leaves" }, { value: "death", label: "Deaths" },
];

export function ActivityPage() {
  const online = useQuery({ queryKey: ["activity", "online"], queryFn: panel.activityOnline, refetchInterval: 10_000 });
  const players = useQuery({ queryKey: ["activity", "players"], queryFn: panel.activityPlayers, refetchInterval: 30_000 });
  return (
    <Page title="Player activity" wide sub="Who is on now, who came and went, and how long each player plays.">
      <ConnectionNote online={online.data} />
      <div className="wa-grid">
        <OnlineCard q={online} />
        <EventsCard players={(players.data?.players ?? []).map((p) => ({ key: p.key, name: p.name }))} />
      </div>
      <Card title="Playtime" sub={players.data?.since ? `Counted from ${when(players.data.since)}: the newest 5,000 joins, leaves and deaths are kept.` : "Counted from the joins, leaves and deaths the panel kept."}>
        {players.isError ? <LoadError what="Playtime" error={players.error} retry={() => void players.refetch()} />
          : players.isLoading ? <Loading />
          : !players.data?.players.length ? <Empty title="No one has played yet">Players show up here after their first join while the panel runs.</Empty>
          : (
            <div className="wa-table-wrap">
              <table className="wa-table wa-table-compact">
                <thead><tr><th>Player</th><th>Now</th><th>Sessions</th><th>Last 7 days</th><th>Last 30 days</th><th>Deaths</th><th>First seen</th></tr></thead>
                <tbody>{players.data.players.map((p) => (
                  <tr key={p.key}>
                    <td data-label="Player"><span className="wa-person-name">{p.name}</span> {p.you && <Pill tone="ember">This is you</Pill>}</td>
                    <td data-label="Now">{p.online ? <Pill tone="ok">online</Pill> : <span className="k-muted" title={when(p.lastSeen)}>last seen {ago(p.lastSeen)}</span>}</td>
                    <td data-label="Sessions" className="wa-num-cell">{p.sessions}</td>
                    <td data-label="Last 7 days" className="wa-num-cell">{duration(p.seconds7)}</td>
                    <td data-label="Last 30 days" className="wa-num-cell">{duration(p.seconds30)}</td>
                    <td data-label="Deaths" className="wa-num-cell">{p.deaths}</td>
                    <td data-label="First seen" className="wa-nowrap">{when(p.firstSeen)}</td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
          )}
      </Card>
    </Page>
  );
}

function ConnectionNote({ online }: { online: ActivityOnline | undefined }) {
  if (!online) return null;
  if (!online.connected) return <Banner tone="warn" title="Not connected to the server">The list shows who was on when the panel last heard from Kiln.Admin.</Banner>;
  if (!online.live) return <Banner tone="info" title="Waiting for the server's first player list">Who is on appears within a few seconds.</Banner>;
  return null;
}

function OnlineCard({ q }: { q: UseQueryResult<ActivityOnline> }) {
  const list = q.data?.players ?? [];
  return (
    <Card title="Online now" actions={q.data && <Pill tone={list.length ? "ok" : "idle"}>{list.length} online</Pill>}>
      {q.isError ? <LoadError what="Who is on" error={q.error} retry={() => void q.refetch()} />
        : q.isLoading ? <Loading />
        : !list.length ? <Empty title="Nobody is on right now">Joins show up here and in the list beside it as they happen.</Empty>
        : <OnlineList players={list} />}
    </Card>
  );
}

export function OnlineList({ players }: { players: OnlinePlayer[] }) {
  return (
    <ul className="wa-people" aria-label="Players online">
      {players.map((p) => (
        <li key={p.key} className="wa-person">
          <span className="wa-dot" aria-hidden />
          <span className="wa-person-name">{p.name}</span>
          {p.you && <Pill tone="ember">This is you</Pill>}
          <span className="wa-person-meta" title={`since ${when(p.since)}`}>on for {duration(p.seconds)}</span>
        </li>
      ))}
    </ul>
  );
}

function EventsCard({ players }: { players: { key: string; name: string }[] }) {
  const [type, setType] = useState<ActivityType | "">("");
  const [player, setPlayer] = useState("");
  const q = useInfiniteQuery({
    queryKey: ["activity", "events", type, player],
    initialPageParam: null as string | null,
    queryFn: ({ pageParam }) => panel.activityEvents({ limit: PAGE, before: pageParam ?? undefined, type, player: player || undefined }),
    getNextPageParam: (last) => (last.more ? last.events[last.events.length - 1]?.at ?? null : null),
    refetchInterval: 15_000,
  });
  const events = useMemo(() => q.data?.pages.flatMap((p) => p.events) ?? [], [q.data]);
  const filtered = type !== "" || player !== "";
  return (
    <Card title="Recent" sub="Newest first.">
      <div className="wa-filters">
        <SelectField label="Show" value={type} onChange={setType} options={TYPES} />
        <SelectField label="Player" value={player} onChange={setPlayer} options={[{ value: "", label: "Everyone" }, ...players.map((p) => ({ value: p.key, label: p.name }))]} />
        {filtered && <Button kind="ghost" onClick={() => { setType(""); setPlayer(""); }}>Clear filter</Button>}
      </div>
      <div aria-live="polite">
        {q.isError ? <LoadError what="The activity log" error={q.error} retry={() => void q.refetch()} />
          : q.isLoading ? <Loading />
          : !events.length ? (filtered ? <Empty title="Nothing matches this filter">Choose another kind or player, or clear the filter.</Empty>
            : <Empty title="Nothing has happened yet">Joins, leaves and deaths appear here while the panel runs.</Empty>)
          : <EventList events={events} />}
      </div>
      {q.hasNextPage && <div className="wa-row"><Button onClick={() => void q.fetchNextPage()} busy={q.isFetchingNextPage}>Older</Button></div>}
    </Card>
  );
}

export function EventList({ events }: { events: ActivityEvent[] }) {
  return (
    <ul className="wa-events" aria-label="Joins, leaves and deaths">
      {events.map((e, i) => (
        <li key={`${e.at}-${e.key}-${e.type}-${i}`} className={`wa-event wa-event-${e.type}`}>
          <span className="wa-event-icon" aria-hidden>{e.type === "join" ? <LogIn size={14} /> : e.type === "leave" ? <LogOut size={14} /> : <Skull size={14} />}</span>
          <span className="wa-event-text">{eventText(e)}</span>
          <time className="wa-event-time" dateTime={e.at} title={when(e.at)}>{ago(e.at)}</time>
        </li>
      ))}
    </ul>
  );
}
