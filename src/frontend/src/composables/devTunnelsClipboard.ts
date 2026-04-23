const USERNAME_PREFIXES = ['amber', 'cinder', 'ember', 'indigo', 'lumen', 'moss', 'nova', 'onyx']
const USERNAME_SUFFIXES = ['badger', 'falcon', 'fox', 'otter', 'signal', 'sparrow', 'voyager', 'warden']
const USER_ROLES = ['admin', 'analyst', 'maintainer', 'operator', 'reviewer']

function pickRandom<T>(items: T[]): T {
  return items[Math.floor(Math.random() * items.length)]
}

function shellQuote(value: string): string {
  return `'${value.replaceAll("'", "'\"'\"'")}'`
}

function buildRandomUsername(): string {
  return `${pickRandom(USERNAME_PREFIXES)}-${pickRandom(USERNAME_SUFFIXES)}-${Math.floor(Math.random() * 900 + 100)}`
}

export function buildDevTunnelSampleCurlCommand(webhookUrl: string): string {
  const payload = {
    user: {
      username: buildRandomUsername(),
      role: pickRandom(USER_ROLES),
    },
    datetime: new Date().toISOString(),
    event_type: 'test',
  }

  return [
    `curl -X POST ${shellQuote(webhookUrl)} \\`,
    `  -H ${shellQuote('Content-Type: application/json')} \\`,
    `  -H ${shellQuote('Accept: application/json')} \\`,
    `  --data-raw ${shellQuote(JSON.stringify(payload))}`,
  ].join('\n')
}

export async function copyTextToClipboard(value: string): Promise<boolean> {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(value)
      return true
    }
  } catch {
    // fall through to legacy path
  }

  try {
    const ta = document.createElement('textarea')
    ta.value = value
    ta.setAttribute('readonly', '')
    ta.style.position = 'fixed'
    ta.style.top = '-1000px'
    ta.style.opacity = '0'
    document.body.appendChild(ta)
    ta.select()
    ta.setSelectionRange(0, value.length)
    const ok = document.execCommand('copy')
    document.body.removeChild(ta)
    return ok
  } catch {
    return false
  }
}
