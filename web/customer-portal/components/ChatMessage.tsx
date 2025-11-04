import type { Message } from '@/types';
import { formatRelativeTime } from '@/lib/utils';
import { Bot, User } from 'lucide-react';
import { CostEstimate } from './CostEstimate';
import { cn } from '@/lib/utils';

interface ChatMessageProps {
  message: Message;
  showTimestamp?: boolean;
}

export function ChatMessage({ message, showTimestamp = false }: ChatMessageProps) {
  const isUser = message.role === 'user';

  return (
    <div
      className={cn(
        'flex gap-3 mb-4',
        isUser ? 'flex-row-reverse' : 'flex-row'
      )}
    >
      <div
        className={cn(
          'flex h-8 w-8 shrink-0 select-none items-center justify-center rounded-full border',
          isUser
            ? 'bg-primary text-primary-foreground'
            : 'bg-secondary text-secondary-foreground'
        )}
      >
        {isUser ? <User className="h-4 w-4" /> : <Bot className="h-4 w-4" />}
      </div>

      <div className={cn('flex flex-col gap-2', isUser ? 'items-end' : 'items-start', 'flex-1')}>
        <div
          className={cn(
            'rounded-lg px-4 py-3 max-w-[80%]',
            isUser
              ? 'bg-primary text-primary-foreground'
              : 'bg-muted text-muted-foreground'
          )}
        >
          <div className="prose prose-sm dark:prose-invert max-w-none">
            <p className="whitespace-pre-wrap break-words m-0">{message.content}</p>
          </div>
        </div>

        {message.metadata?.costEstimate && (
          <div className="max-w-[80%] w-full">
            <CostEstimate estimate={message.metadata.costEstimate} />
          </div>
        )}

        {showTimestamp && message.timestamp && (
          <span className="text-xs text-muted-foreground">
            {formatRelativeTime(message.timestamp)}
          </span>
        )}
      </div>
    </div>
  );
}
