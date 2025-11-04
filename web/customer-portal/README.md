# Honua Customer Portal

A modern, React-based customer portal for managing Honua Server container builds, registry credentials, and licenses.

## Features

- **Dashboard**: Real-time build statistics, success rates, and cache hit metrics
- **AI-Guided Intake**: Conversational interface for creating custom builds
- **Build Management**: View history, download artifacts, monitor status
- **Registry Credentials**: Manage ECR, ACR, GCR, and GHCR access
- **License Management**: Track usage quotas and plan features
- **Dark Mode**: Full theme support with system preference detection
- **Real-time Updates**: WebSocket integration for live build status

## Tech Stack

- **Framework**: Next.js 14 (App Router)
- **Language**: TypeScript
- **Styling**: Tailwind CSS
- **UI Components**: ShadCN UI
- **Data Fetching**: React Query
- **Charts**: Recharts
- **Icons**: Lucide React

## Getting Started

### Prerequisites

- Node.js 18+ and npm 9+
- Honua Server API running (default: http://localhost:5000)

### Installation

```bash
# Install dependencies
npm install

# Copy environment variables
cp .env.example .env.local

# Update API URL in .env.local
NEXT_PUBLIC_API_URL=http://localhost:5000
NEXT_PUBLIC_WS_URL=ws://localhost:5000
```

### Development

```bash
# Start development server
npm run dev

# Open http://localhost:3000
```

### Production Build

```bash
# Build for production
npm run build

# Start production server
npm start
```

### Type Checking

```bash
npm run type-check
```

### Linting

```bash
npm run lint
```

## Project Structure

```
web/customer-portal/
├── app/                      # Next.js App Router pages
│   ├── page.tsx             # Dashboard home
│   ├── intake/              # AI intake conversation
│   ├── builds/              # Build history
│   ├── license/             # License management
│   ├── registries/          # Registry credentials
│   ├── layout.tsx           # Root layout with navigation
│   └── providers.tsx        # React Query & theme providers
├── components/              # Reusable components
│   ├── ui/                  # ShadCN UI components
│   ├── BuildCard.tsx        # Build status card
│   ├── BuildStatusBadge.tsx # Status indicator
│   ├── ChatMessage.tsx      # AI message display
│   ├── CostEstimate.tsx     # Cost breakdown
│   └── RegistryCard.tsx     # Registry info card
├── lib/                     # Utilities and API client
│   ├── api.ts              # Typed API client
│   ├── utils.ts            # Helper functions
│   └── websocket.ts        # WebSocket client
├── types/                   # TypeScript type definitions
│   └── index.ts            # All application types
├── package.json            # Dependencies
├── tsconfig.json           # TypeScript config
├── tailwind.config.ts      # Tailwind configuration
└── next.config.js          # Next.js configuration
```

## API Integration

The portal communicates with the Honua Server API via the typed client in `/lib/api.ts`:

### Endpoints

- `GET /api/dashboard/stats` - Dashboard statistics
- `GET /api/builds` - List builds with filters
- `GET /api/builds/:id` - Get build details
- `POST /api/builds/:id/download` - Download build artifact
- `POST /api/intake/conversations` - Create AI conversation
- `POST /api/intake/conversations/:id/messages` - Send message
- `GET /api/license` - Get license details
- `GET /api/registries` - List registry credentials
- `POST /api/registries/:id/rotate` - Rotate credentials

### WebSocket Events

- `build_update` - Real-time build progress
- `build_complete` - Build finished successfully
- `build_failed` - Build failed with error
- `queue_update` - Queue position changed

## Key Features

### AI-Guided Build Intake

Interactive chat interface that guides users through:
- Framework and language selection
- Architecture requirements (amd64, arm64, multi)
- Custom dependencies and configurations
- Cost estimation
- Build preview before execution

### Build Management

- Filterable build history
- Real-time status updates via WebSocket
- Download completed builds
- Retry failed builds
- Cancel queued/running builds
- Build logs and metadata

### Registry Credentials

- Multi-cloud registry support (ECR, ACR, GCR, GHCR)
- Secure credential display with copy-to-clipboard
- Connection instructions per registry type
- Credential rotation
- Connection testing
- Usage tracking

### License Management

- Usage quotas with visual progress bars
- Feature availability by tier
- Expiration warnings
- Upgrade options
- Billing information

## Customization

### Theme Colors

Edit `app/globals.css` to customize color schemes for light and dark modes.

### API Configuration

Update `NEXT_PUBLIC_API_URL` in `.env.local` to point to your Honua Server instance.

### Feature Flags

Enable/disable features via environment variables:

```env
NEXT_PUBLIC_ENABLE_WEBSOCKETS=true
NEXT_PUBLIC_ENABLE_DARK_MODE=true
```

## Deployment

### Docker

```bash
# Build Docker image
docker build -t honua-portal .

# Run container
docker run -p 3000:3000 -e NEXT_PUBLIC_API_URL=https://api.honua.io honua-portal
```

### Vercel

```bash
# Install Vercel CLI
npm i -g vercel

# Deploy
vercel
```

### Static Export

```bash
# Build static site
npm run build
npm run export

# Deploy to any static host
```

## Browser Support

- Chrome/Edge: Last 2 versions
- Firefox: Last 2 versions
- Safari: Last 2 versions

## Contributing

This is an internal Honua Server component. For issues or improvements, contact the development team.

## License

Proprietary - Honua Server
