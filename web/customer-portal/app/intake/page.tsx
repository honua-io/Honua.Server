'use client';

import { useState, useRef, useEffect } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ChatMessage } from '@/components/ChatMessage';
import { useToast } from '@/components/ui/use-toast';
import { useRouter } from 'next/navigation';
import type { Message, BuildConfig } from '@/types';
import { Send, Loader2, Sparkles, Rocket } from 'lucide-react';

export default function IntakePage() {
  const router = useRouter();
  const { toast } = useToast();
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');
  const [buildConfig, setBuildConfig] = useState<BuildConfig | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  // Create conversation on mount
  const { isLoading: isCreating } = useQuery({
    queryKey: ['create-conversation'],
    queryFn: async () => {
      const conversation = await api.createConversation();
      setConversationId(conversation.id);

      // Add initial message
      setMessages([
        {
          id: '1',
          role: 'assistant',
          content:
            "Hello! I'm here to help you create a custom container build. I'll guide you through the process by asking a few questions about your requirements.\n\nTo get started, could you tell me:\n1. What programming language or framework will you be using?\n2. What's the primary purpose of this container?\n3. Do you have any specific requirements or constraints?",
          timestamp: new Date().toISOString(),
        },
      ]);

      return conversation;
    },
    enabled: !conversationId,
  });

  const sendMessage = useMutation({
    mutationFn: async (message: string) => {
      if (!conversationId) throw new Error('No conversation');
      return api.sendIntakeMessage(conversationId, message);
    },
    onSuccess: (data) => {
      const userMessage: Message = {
        id: Date.now().toString(),
        role: 'user',
        content: input,
        timestamp: new Date().toISOString(),
      };

      const assistantMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: data.reply,
        timestamp: new Date().toISOString(),
        metadata: {
          costEstimate: data.costEstimate,
          buildPreview: data.buildConfig,
        },
      };

      setMessages((prev) => [...prev, userMessage, assistantMessage]);
      setInput('');

      if (data.buildReady && data.buildConfig) {
        setBuildConfig(data.buildConfig);
        toast({
          title: 'Build Configuration Ready',
          description: 'Your build configuration is ready. Review it and start the build when ready.',
        });
      }
    },
    onError: (error) => {
      toast({
        title: 'Error',
        description: error instanceof Error ? error.message : 'Failed to send message',
        variant: 'destructive',
      });
    },
  });

  const startBuild = useMutation({
    mutationFn: async () => {
      if (!conversationId) throw new Error('No conversation');
      return api.startBuildFromConversation(conversationId);
    },
    onSuccess: (build) => {
      toast({
        title: 'Build Started',
        description: `Build ${build.id} has been queued successfully.`,
      });
      router.push(`/builds?id=${build.id}`);
    },
    onError: (error) => {
      toast({
        title: 'Build Failed',
        description: error instanceof Error ? error.message : 'Failed to start build',
        variant: 'destructive',
      });
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!input.trim() || sendMessage.isPending) return;
    sendMessage.mutate(input);
  };

  if (isCreating) {
    return (
      <div className="container mx-auto p-6 flex items-center justify-center min-h-[60vh]">
        <div className="text-center">
          <Loader2 className="h-8 w-8 animate-spin mx-auto mb-4" />
          <p className="text-muted-foreground">Starting conversation...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto p-6 max-w-5xl">
      <div className="mb-6">
        <h1 className="text-3xl font-bold flex items-center gap-2">
          <Sparkles className="h-8 w-8 text-primary" />
          AI Build Assistant
        </h1>
        <p className="text-muted-foreground mt-1">
          Let our AI guide you through creating the perfect container build
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Chat Area */}
        <Card className="lg:col-span-2 flex flex-col h-[600px]">
          <CardContent className="flex-1 overflow-y-auto p-4 space-y-4">
            {messages.map((message, index) => (
              <ChatMessage key={message.id || index} message={message} />
            ))}
            {sendMessage.isPending && (
              <div className="flex items-center gap-2 text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                <span className="text-sm">Thinking...</span>
              </div>
            )}
            <div ref={messagesEndRef} />
          </CardContent>

          <div className="border-t p-4">
            <form onSubmit={handleSubmit} className="flex gap-2">
              <Input
                value={input}
                onChange={(e) => setInput(e.target.value)}
                placeholder="Describe your requirements..."
                disabled={sendMessage.isPending}
                className="flex-1"
              />
              <Button
                type="submit"
                disabled={!input.trim() || sendMessage.isPending}
              >
                {sendMessage.isPending ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Send className="h-4 w-4" />
                )}
              </Button>
            </form>
          </div>
        </Card>

        {/* Build Preview */}
        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Build Preview</CardTitle>
            </CardHeader>
            <CardContent>
              {buildConfig ? (
                <div className="space-y-4">
                  <div>
                    <label className="text-sm font-medium text-muted-foreground">
                      Configuration Name
                    </label>
                    <p className="font-medium mt-1">{buildConfig.name}</p>
                  </div>

                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className="text-sm font-medium text-muted-foreground">
                        Tier
                      </label>
                      <p className="font-medium mt-1 capitalize">
                        {buildConfig.tier}
                      </p>
                    </div>
                    <div>
                      <label className="text-sm font-medium text-muted-foreground">
                        Architecture
                      </label>
                      <p className="font-medium mt-1">{buildConfig.architecture}</p>
                    </div>
                  </div>

                  {buildConfig.frameworks.length > 0 && (
                    <div>
                      <label className="text-sm font-medium text-muted-foreground">
                        Frameworks
                      </label>
                      <div className="flex flex-wrap gap-1 mt-2">
                        {buildConfig.frameworks.map((framework) => (
                          <span
                            key={framework}
                            className="inline-flex items-center px-2 py-0.5 rounded text-xs bg-secondary"
                          >
                            {framework}
                          </span>
                        ))}
                      </div>
                    </div>
                  )}

                  {buildConfig.runtime && (
                    <div>
                      <label className="text-sm font-medium text-muted-foreground">
                        Runtime
                      </label>
                      <p className="font-medium mt-1">{buildConfig.runtime}</p>
                    </div>
                  )}

                  <div className="pt-3 border-t">
                    <div className="flex justify-between text-sm mb-1">
                      <span className="text-muted-foreground">
                        Estimated Size
                      </span>
                      <span className="font-medium">
                        {buildConfig.estimatedSizeMb} MB
                      </span>
                    </div>
                    <div className="flex justify-between text-sm">
                      <span className="text-muted-foreground">
                        Build Time
                      </span>
                      <span className="font-medium">
                        {buildConfig.estimatedBuildTime}
                      </span>
                    </div>
                  </div>

                  <Button
                    className="w-full"
                    onClick={() => startBuild.mutate()}
                    disabled={startBuild.isPending}
                  >
                    {startBuild.isPending ? (
                      <>
                        <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                        Starting Build...
                      </>
                    ) : (
                      <>
                        <Rocket className="h-4 w-4 mr-2" />
                        Start Build
                      </>
                    )}
                  </Button>
                </div>
              ) : (
                <div className="text-center py-8 text-muted-foreground">
                  <p className="text-sm">
                    Continue the conversation to generate your build configuration
                  </p>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Quick Tips</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2 text-sm text-muted-foreground">
              <p>• Be specific about your requirements</p>
              <p>• Mention any version constraints</p>
              <p>• List required system dependencies</p>
              <p>• Describe your deployment target</p>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
