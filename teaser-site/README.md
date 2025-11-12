# Honua Teaser Site

A modern, responsive landing page for Honua - the cloud-native geospatial server.

## Overview

This teaser site is designed to build anticipation and collect email signups for Honua's launch without revealing the GitHub repository URL yet.

## What's Included

- **Clean, modern design** with gradient accents and smooth animations
- **Email newsletter signup form** as the main CTA
- **Feature showcase** highlighting 9 key capabilities
- **Statistics section** with impressive numbers (10+ OGC standards, 11+ databases, etc.)
- **Fully responsive** design that works on all devices
- **Fast loading** with embedded CSS and minimal dependencies
- **Easy to customize** with CSS custom properties

## Key Features Highlighted

1. Serverless-Ready Architecture
2. Standards-First Approach (OGC compliance)
3. Zero Vendor Lock-In
4. Real-Time Collaboration
5. Mobile-First Design
6. Visual Map Builder
7. Advanced Analytics (40+ spatial operations)
8. Enterprise Security
9. Full Observability

## Quick Start

### Option 1: View Locally
1. Open `index.html` in any modern web browser
2. That's it! No build process required.

### Option 2: Import to Webflow
See `WEBFLOW-INSTRUCTIONS.md` for detailed steps on importing this into Webflow.

### Option 3: Deploy to Any Static Host
Upload `index.html` to:
- Netlify (drag & drop)
- Vercel
- GitHub Pages
- AWS S3 + CloudFront
- Any static hosting service

## Customization

### Change Colors
Edit the CSS custom properties in the `<style>` section:
```css
:root {
    --primary-color: #2563eb;
    --primary-dark: #1e40af;
    /* ... etc */
}
```

### Update Content
- Headline: Line 344
- Subtitle: Line 349
- Features: Lines 461-533
- Stats: Lines 402-419
- Footer: Line 551

### Newsletter Integration
The form is ready to connect to:
- Webflow Forms
- Mailchimp
- ConvertKit
- Custom webhook/API
- Any email service provider

See `WEBFLOW-INSTRUCTIONS.md` for integration details.

## Technical Details

- **Framework**: Pure HTML/CSS/JavaScript (no dependencies)
- **Font**: Google Fonts - Inter (weights: 400-800)
- **Browser Support**: All modern browsers (Chrome, Firefox, Safari, Edge)
- **Mobile Responsive**: Yes, fully responsive
- **Accessibility**: Semantic HTML, proper form labels
- **Performance**: ~10KB gzipped, <100ms load time

## Files

```
teaser-site/
├── index.html                    # Main landing page
├── WEBFLOW-INSTRUCTIONS.md       # Webflow import guide
└── README.md                     # This file
```

## Design Philosophy

The teaser site follows these principles:
- **Simple & Clean**: Focus on the message without distractions
- **Professional**: Enterprise-grade design that builds trust
- **Modern**: Contemporary design patterns and smooth interactions
- **Fast**: No bloated frameworks, just clean code
- **Accessible**: Works for everyone, on every device

## Newsletter Form Behavior

The form currently includes:
- Email validation (HTML5 `required` attribute)
- Placeholder text for guidance
- Submit button with hover effects
- Mobile-responsive layout

You'll need to configure the form action to connect it to your email service provider.

## Upcoming Enhancements

Future versions could include:
- Success/error message handling
- reCAPTCHA integration
- Social proof (testimonials, user count)
- FAQ section
- Video teaser
- Screenshot carousel

## Support

For questions about:
- **Webflow**: See `WEBFLOW-INSTRUCTIONS.md`
- **Customization**: Edit the HTML/CSS directly
- **Deployment**: Check your hosting provider's documentation

## License

This teaser site is part of the Honua project.

---

**Ready to launch?** Follow the instructions in `WEBFLOW-INSTRUCTIONS.md` to get started with Webflow, or simply open `index.html` in a browser to preview! 🚀
