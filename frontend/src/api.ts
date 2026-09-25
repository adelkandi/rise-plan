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

export type TaskResponse = {
  id: string
  title: string
  description?: string
  status: string
  priority: string
  estimatedMinutes?: number
  dueDate?: string
  completedAt?: string
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

export async function createTask(title: string, description?: string): Promise<TaskResponse> {
  const response = await fetch(`${apiBaseUrl}/api/tasks`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ title, description, priority: 'Normal' }),
  })

  if (!response.ok) {
    throw new Error(`Creating the task failed with status ${response.status}.`)
  }

  return response.json() as Promise<TaskResponse>
}
