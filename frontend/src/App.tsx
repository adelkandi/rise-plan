import { useState } from 'react'
import type { FormEvent } from 'react'
import type { PlanningSuggestion } from './api'
import { sendChatMessage } from './api'
import './App.css'

type ChatMessage = {
  id: number
  role: 'rise' | 'user'
  content: string
  suggestions?: PlanningSuggestion[]
}

const initialMessage: ChatMessage = {
  id: 1,
  role: 'rise',
  content:
    'Good morning. Tell me what is on your mind. You do not need to organize it first.',
}

const prompts = ['Plan my day', 'I feel overwhelmed', 'What should I focus on?']

function App() {
  const [conversationId, setConversationId] = useState<string | null>(null)
  const [messages, setMessages] = useState<ChatMessage[]>([initialMessage])
  const [draft, setDraft] = useState('')
  const [isSending, setIsSending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submitMessage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const content = draft.trim()
    if (!content || isSending) return

    setDraft('')
    setError(null)
    setMessages((current) => [...current, { id: Date.now(), role: 'user', content }])
    setIsSending(true)

    try {
      const response = await sendChatMessage(conversationId, content)
      setConversationId(response.conversationId)
      setMessages((current) => [
        ...current,
        {
          id: Date.now() + 1,
          role: 'rise',
          content: response.planning.content,
          suggestions: response.planning.suggestions,
        },
      ])
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Something went wrong.')
    } finally {
      setIsSending(false)
    }
  }

  return (
    <main className="app-shell">
      <header className="topbar">
        <a className="brand" href="/" aria-label="Rise home">
          <span className="brand-mark" aria-hidden="true">R</span>
          <span>rise</span>
        </a>
        <span className="status"><span className="status-dot" />Local planner</span>
      </header>

      <section className="conversation" aria-labelledby="greeting">
        <div className="conversation-heading">
          <p className="eyebrow">Your morning companion</p>
          <h1 id="greeting">What is on your mind?</h1>
          <p className="subtitle">Start anywhere. Rise will help you find what matters without over-planning your day.</p>
        </div>

        <div className="message-list" aria-live="polite">
          {messages.map((message) => (
            <article className={`message message-${message.role}`} key={message.id}>
              {message.role === 'rise' && <span className="message-label">Rise</span>}
              <p>{message.content}</p>
              {message.suggestions && message.suggestions.length > 0 && (
                <div className="response-suggestions">
                  {message.suggestions.map((suggestion) => (
                    <div className="response-suggestion" key={`${message.id}-${suggestion.title}`}>
                      <strong>{suggestion.title}</strong>
                      {suggestion.description && <span>{suggestion.description}</span>}
                    </div>
                  ))}
                </div>
              )}
            </article>
          ))}
          {isSending && <p className="typing">Rise is thinking<span>...</span></p>}
        </div>

        <div className="prompt-list" aria-label="Suggested prompts">
          {prompts.map((prompt) => (
            <button type="button" className="prompt" key={prompt} onClick={() => setDraft(prompt)}>
              {prompt}
            </button>
          ))}
        </div>

        <form className="composer" onSubmit={submitMessage}>
          <label className="sr-only" htmlFor="message">Message Rise</label>
          <textarea
            id="message"
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder="Tell Rise what is on your mind..."
            rows={1}
            disabled={isSending}
          />
          <button className="send-button" type="submit" disabled={isSending || !draft.trim()} aria-label="Send message">
            ↑
          </button>
        </form>
        {error && <p className="error-message" role="alert">{error}</p>}
        <p className="composer-hint">Your thoughts stay yours. You stay in control.</p>
      </section>
    </main>
  )
}

export default App
