import Anthropic from '@anthropic-ai/sdk'

type SupportedMime = 'image/jpeg' | 'image/png' | 'image/gif' | 'image/webp'

function normalizeMime(mimeType: string): SupportedMime {
  const lower = mimeType.toLowerCase()
  if (lower === 'image/jpeg' || lower === 'image/jpg') return 'image/jpeg'
  if (lower === 'image/png') return 'image/png'
  if (lower === 'image/gif') return 'image/gif'
  if (lower === 'image/webp') return 'image/webp'
  return 'image/jpeg'
}

export async function readNameFromBadge(
  imageBase64: string,
  mimeType: string
): Promise<string> {
  const apiKey = process.env.ANTHROPIC_API_KEY
  if (!apiKey) {
    throw new Error('ANTHROPIC_API_KEY is not set')
  }

  const client = new Anthropic({ apiKey })
  const media = normalizeMime(mimeType)

  const response = await client.messages.create({
    model: 'claude-3-5-haiku-20241022',
    max_tokens: 50,
    messages: [
      {
        role: 'user',
        content: [
          {
            type: 'image',
            source: {
              type: 'base64',
              media_type: media,
              data: imageBase64,
            },
          },
          {
            type: 'text',
            text:
              "This is a conference name badge. Read the attendee's full name exactly as printed on the badge. " +
              'Return ONLY the name, nothing else. If you cannot clearly read a name, return exactly: Unknown',
          },
        ],
      },
    ],
  })

  const first = response.content[0]
  const text = first && first.type === 'text' ? first.text.trim() : 'Unknown'
  return text || 'Unknown'
}
