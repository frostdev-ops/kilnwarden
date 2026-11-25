# Security

Kilnwarden puts an admin panel for your game server on the network, so we take reports seriously and thank everyone
who sends one.

## Reporting a problem

Please report security problems **privately**, not in a public issue:

- **GitHub:** use [Report a vulnerability](https://github.com/frostdev-ops/kilnwarden/security/advisories/new) on this
  repository (a private security advisory), or
- **Discord:** send a direct message to a moderator on the [Kiln Discord](https://discord.gg/7pjr8SMTfU) and say it
  is a security report. Don't post the details in a public channel.

Tell us what you found, how to reproduce it, and which version you run (Kilnwarden and Kiln). Leave out passwords,
setup links, claim links and the contents of `kiln/data/kilnwarden/`: we never need them.

We answer within a few days, keep you posted while we fix it, and credit you in the release notes unless you'd rather
we didn't.

## What counts

Anything that lets someone do in the panel what their account or role should not allow, read what they should not
read, or reach the server in a way the owner did not turn on. That includes frostdev.io's hosted services for
Kilnwarden (the relay for `<name>.kiln.nexus` and the Discord sign-in broker, both coming soon).

Please don't test against servers or panels that aren't yours, and don't run load tests against frostdev.io or
kiln.nexus. A local panel (see "Building from source" in the README) is the place to try things.

## Supported versions

Only the newest release gets security fixes. Kilnwarden updates through the Kiln Launcher and the panel's own Mods page.
