import { Fragment } from 'react'
import { Link } from 'react-router-dom'
import type { ContentMention } from '../../api/posts'

function HashtaggedText({ text }: { text: string }) {
  const parts = text.split(/(#[\p{L}\p{N}_]{1,50})/gu)
  return <>{parts.map((part, index) => part.startsWith('#') ? (
    <Link key={index} to={`/hashtag/${encodeURIComponent(part.slice(1).toLowerCase())}`} className="font-medium text-primary no-underline hover:underline">{part}</Link>
  ) : <Fragment key={index}>{part}</Fragment>)}</>
}

interface TextWithReferencesProps {
  content: string
  mentions?: readonly ContentMention[] | null
  className?: string
}

export default function TextWithReferences({ content, mentions = [], className }: TextWithReferencesProps) {
  const validMentions = [...(mentions ?? [])]
    .sort((left, right) => left.startIndex - right.startIndex)
    .filter((mention) => mention.startIndex >= 0 && mention.length > 0 && mention.startIndex + mention.length <= content.length)
  let position = 0
  const nodes: React.ReactNode[] = []

  validMentions.forEach((mention) => {
    if (mention.startIndex < position) return
    if (mention.startIndex > position) {
      nodes.push(<HashtaggedText key={`text-${position}`} text={content.slice(position, mention.startIndex)} />)
    }
    nodes.push(
      <Link key={`mention-${mention.startIndex}-${mention.userId}`} to={`/profile/${mention.userId}`} className="font-medium text-primary no-underline hover:underline">
        {content.slice(mention.startIndex, mention.startIndex + mention.length)}
      </Link>,
    )
    position = mention.startIndex + mention.length
  })

  if (position < content.length) {
    nodes.push(<HashtaggedText key={`text-${position}`} text={content.slice(position)} />)
  }

  return <span className={className}>{nodes}</span>
}
