import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import './globals.css';
import { Providers } from './providers';
import { Toaster } from '@/components/Toaster';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import {
  LayoutDashboard,
  MessageSquare,
  Package,
  Key,
  CreditCard,
  Moon,
  Sun,
} from 'lucide-react';
import { useTheme } from 'next-themes';

const inter = Inter({ subsets: ['latin'] });

export const metadata: Metadata = {
  title: 'Honua Customer Portal',
  description: 'Manage your Honua Server builds, registries, and licenses',
};

function Navigation() {
  return (
    <nav className="border-b">
      <div className="container mx-auto px-4">
        <div className="flex h-16 items-center justify-between">
          <div className="flex items-center gap-8">
            <Link href="/" className="flex items-center gap-2 font-bold text-xl">
              <span className="text-2xl">🌺</span>
              Honua
            </Link>

            <div className="hidden md:flex items-center gap-1">
              <Link href="/">
                <Button variant="ghost" size="sm">
                  <LayoutDashboard className="h-4 w-4 mr-2" />
                  Dashboard
                </Button>
              </Link>
              <Link href="/intake">
                <Button variant="ghost" size="sm">
                  <MessageSquare className="h-4 w-4 mr-2" />
                  New Build
                </Button>
              </Link>
              <Link href="/builds">
                <Button variant="ghost" size="sm">
                  <Package className="h-4 w-4 mr-2" />
                  Builds
                </Button>
              </Link>
              <Link href="/registries">
                <Button variant="ghost" size="sm">
                  <Key className="h-4 w-4 mr-2" />
                  Registries
                </Button>
              </Link>
              <Link href="/license">
                <Button variant="ghost" size="sm">
                  <CreditCard className="h-4 w-4 mr-2" />
                  License
                </Button>
              </Link>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <ThemeToggle />
          </div>
        </div>
      </div>
    </nav>
  );
}

function ThemeToggle() {
  'use client';
  const { theme, setTheme } = useTheme();

  return (
    <Button
      variant="ghost"
      size="icon"
      onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
    >
      <Sun className="h-5 w-5 rotate-0 scale-100 transition-all dark:-rotate-90 dark:scale-0" />
      <Moon className="absolute h-5 w-5 rotate-90 scale-0 transition-all dark:rotate-0 dark:scale-100" />
      <span className="sr-only">Toggle theme</span>
    </Button>
  );
}

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className={inter.className}>
        <Providers>
          <div className="min-h-screen flex flex-col">
            <Navigation />
            <main className="flex-1">{children}</main>
            <footer className="border-t py-6 mt-12">
              <div className="container mx-auto px-4 text-center text-sm text-muted-foreground">
                <p>Honua Server Customer Portal</p>
              </div>
            </footer>
          </div>
          <Toaster />
        </Providers>
      </body>
    </html>
  );
}
