import { getHighlightParts } from './searchPresentation'

export default function HighlightedText({ text, query }: { text: string; query: string }) {
  return <>{getHighlightParts(text, query).map((part, index) => part.match
    ? <span key={index} data-search-highlight className="rounded-sm bg-primary/15 font-semibold text-text">{part.text}</span>
    : part.text)}</>
}
