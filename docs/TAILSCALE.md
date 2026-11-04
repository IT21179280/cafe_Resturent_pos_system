# Remote access with Tailscale

Tailscale is a free, private mesh VPN. Install it on the CafePOS server and on the remote
device (for example the owner's laptop). The remote device then reaches the server through
an encrypted private address without opening port 5080 on the router.

Do not expose the CafePOS API or PostgreSQL directly to the internet.

## 1. Prepare the server

1. Complete the normal CafePOS server installation and confirm
   `http://localhost:5080/health` works on the server.
2. Install Tailscale for Windows from <https://tailscale.com/download>.
3. Sign in with the same Tailscale account you will use for the remote device.
4. In the Tailscale tray menu, note the server's Tailscale IPv4 address, for example
   `100.101.102.103`.
5. Keep the CafePOS firewall rule that allows TCP 5080. The installer creates it. Tailscale
   traffic arrives on the Tailscale interface and is covered by the rule.

You do not need to enable Windows Internet Connection Sharing or a router port forward.

## 2. Prepare the remote client

1. Install CafePOS on the remote Windows device (Client component only).
2. Install Tailscale and sign in with the same tailnet account.
3. Confirm the server appears:
   ```powershell
   tailscale status
   ```
4. Test the API:
   ```powershell
   Invoke-RestMethod http://100.101.102.103:5080/health
   ```
5. Start CafePOS and enter the server address:
   ```
   http://100.101.102.103:5080
   ```
   The client adds `/api/v1` automatically.
6. Sign in with a CafePOS account.

Tailscale encrypts the connection between the devices. The CafePOS API itself remains HTTP;
that is acceptable inside the private tailnet for a small deployment. If you need HTTPS
inside the tailnet, use Tailscale Serve or a TLS reverse proxy in front of the API and enter
the HTTPS URL on the client.

## 3. Optional: HTTPS with Tailscale Serve

If your Tailscale version supports `serve` on Windows, you can publish the local API on the
tailnet with a trusted HTTPS certificate:

```powershell
tailscale serve --bg 5080
tailscale serve status
```

When Serve is configured, clients can use the HTTPS URL shown by `tailscale serve status`,
for example `https://cafepos-server.tailnet-name.ts.net`. The API still listens locally on
port 5080; Serve terminates TLS.

If your Tailscale build does not support Serve on Windows, keep using the encrypted
`http://100.x.y.z:5080` address inside the tailnet.

## 4. Restrict access with ACLs

In the Tailscale admin console, restrict who can reach the server. Example policy fragment:

```json
{
  "acls": [
    {
      "action": "accept",
      "src": ["group:cafepos-owners", "group:cafepos-managers"],
      "dst": ["tag:cafepos-server:5080"]
    }
  ],
  "groups": {
    "group:cafepos-owners": ["owner@example.com"],
    "group:cafepos-managers": ["manager@example.com"]
  },
  "tagOwners": {
    "tag:cafepos-server": ["group:cafepos-owners"]
  }
}
```

Apply the tag `tag:cafepos-server` to the server device. Do not grant cafe clients access to
other devices in the tailnet unless it is needed.

## 5. Safety rules

- Keep PostgreSQL bound to localhost; Tailscale should expose only the CafePOS API.
- Disable or remove Tailscale access for former staff.
- Use strong, unique CafePOS passwords for remote users.
- Review the audit log for unusual remote activity.
- Keep the server's Tailscale client and Windows updates current.
- If the server IP changes, update the client server address in **Settings -> Device**.
  MagicDNS names are more stable than the raw 100.x address if enabled.
