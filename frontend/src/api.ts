export type PlanningSuggestion = {
  type: string
  title: string
  description?: string
}

export type PlanningResponse = {
  type: string
  content: string
  suggestions: PlanningSuggestion[]
}

export type ChatMessageResponse = {
  conversationId: string
  planning: PlanningResponse
}

const apiBaseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5211'

export async function sendChatMessage(
  conversationId: string | null,
  message: string,
): Promise<ChatMessageResponse> {
  const response = await fetch(`${apiBaseUrl}/api/chat/message`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ conversationId, message }),
  })

  if (!response.ok) {
    throw new Error(`The chat request failed with status ${response.status}.`)
  }

  return response.json() as Promise<ChatMessageResponse>
}
