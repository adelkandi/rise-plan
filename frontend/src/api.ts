export type PlanningSuggestion = {
  type: string
  title: string
  description?: string
  startTime?: string
  endTime?: string
  location?: string
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

export type CalendarConnectionResponse = {
  provider: string
  connected: boolean
}

export type CalendarEvent = {
  id: string
  userId: string
  title: string
  description?: string
  startTime: string
  endTime: string
  location?: string
}

export type PlanItem = {
  id: string
  taskId?: string
  commitmentId?: string
  title: string
  description?: string
  startTime?: string
  endTime?: string
  type: string
  status: string
  priority: string
}

export type DailyPlan = {
  id: string
  date: string
  summary?: string
  items: PlanItem[]
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

export async function getCalendarConnection(): Promise<CalendarConnectionResponse> {
  const response = await fetch(`${apiBaseUrl}/api/calendar/connection`)
  if (!response.ok) {
    throw new Error(`Checking calendar connection failed with status ${response.status}.`)
  }
  return response.json() as Promise<CalendarConnectionResponse>
}

export function getGoogleCalendarConnectUrl(): string {
  return `${apiBaseUrl}/api/calendar/connect/google`
}

export async function disconnectCalendar(): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/calendar/connection`, { method: 'DELETE' })
  if (!response.ok) {
    throw new Error(`Disconnecting the calendar failed with status ${response.status}.`)
  }
}

export async function getCalendarEvents(
  from: Date,
  to: Date,
): Promise<CalendarEvent[]> {
  const query = new URLSearchParams({
    from: from.toISOString(),
    to: to.toISOString(),
  })
  const response = await fetch(`${apiBaseUrl}/api/calendar/events?${query}`)
  if (!response.ok) {
    throw new Error(`Loading calendar events failed with status ${response.status}.`)
  }
  return response.json() as Promise<CalendarEvent[]>
}

export async function createCalendarEvent(event: {
  title: string
  description?: string
  startTime: string
  endTime: string
  location?: string
}): Promise<CalendarEvent> {
  const response = await fetch(`${apiBaseUrl}/api/calendar/events`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...event, confirmed: true }),
  })
  if (!response.ok) {
    throw new Error(`Creating the calendar event failed with status ${response.status}.`)
  }
  return response.json() as Promise<CalendarEvent>
}

export async function generateDailyPlan(date: string): Promise<DailyPlan> {
  const response = await fetch(`${apiBaseUrl}/api/plans/${date}/generate`, { method: 'POST' })
  if (!response.ok) {
    throw new Error(`Generating the daily plan failed with status ${response.status}.`)
  }
  return response.json() as Promise<DailyPlan>
}
