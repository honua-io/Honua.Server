# HonuaIO Teaser Site - Webflow Import Instructions

This guide will help you import the HonuaIO teaser site into Webflow.

## Quick Overview

The teaser site includes:
- Modern, responsive landing page design
- Email newsletter signup form
- Feature showcase with 9 key features (GeoETL, GeoEvents, Geoprocessing, 3D, AI)
- Statistics section highlighting HonuaIO's capabilities (10+ OGC standards, 11+ databases, <30MB plugins)
- Mobile-responsive design
- Clean, professional styling

## Option 1: Use as a Static Page (Recommended for Quick Setup)

### Step 1: Create a New Page in Webflow
1. Log into your Webflow project
2. Go to **Pages** panel
3. Click the **+** button to add a new page
4. Name it "Home" or "Landing"

### Step 2: Add an Embed Element
1. In the Webflow Designer, drag an **Embed** component onto your page
2. Paste the entire contents of `index.html` into the embed
3. Click **Save & Close**

### Step 3: Publish
1. Click **Publish** in the top right
2. Your teaser site is now live!

---

## Option 2: Rebuild in Webflow Designer (Recommended for Full Customization)

This method gives you complete control over the design in Webflow's visual editor.

### Step 1: Set Up Custom Fonts
1. Go to **Site Settings** > **Fonts**
2. Add Google Font: **Inter** (weights: 400, 500, 600, 700, 800)
3. Set it as your body font

### Step 2: Add Custom CSS
1. Go to **Site Settings** > **Custom Code**
2. In the **Head Code** section, paste the CSS from the `<style>` tag in index.html
3. Save

### Step 3: Build the Structure

#### Header Section
1. Add a **Section** element
2. Inside: Add a **Container** (max-width: 1200px)
3. Add a **Div Block** with flex display (space-between)
4. Left side: Add **Heading** with text "HonuaIO" (class: `logo`)
5. Right side: Add **Div** with text "In Development" (class: `status-badge`)

#### Hero Section
1. Add a new **Section** (class: `hero`)
2. Add **Container** inside
3. Add elements in this order:
   - **Text Block**: "COMING SOON" (class: `hero-tagline`)
   - **Heading H1**: Your headline (use `gradient-text` class for colored text)
   - **Paragraph**: Subtitle text (class: `hero-subtitle`)

#### Newsletter Form
1. Inside hero container, add **Div Block** (class: `newsletter-section`)
2. Add **Heading H2**: "Be the first to know"
3. Add **Paragraph**: Description text
4. Add **Form Block** (Webflow native form)
5. Inside form:
   - Add **Email Input** field (class: `form-input`)
   - Add **Submit Button** (class: `btn-primary`, text: "Notify Me")
6. Style the form wrapper with class `form-group`

#### Stats Section
1. Add **Section** (class: `stats`)
2. Add **Container**
3. Add **Div Block** with CSS Grid (class: `stats-grid`)
4. Add 4 stat items, each with:
   - **Div Block** (class: `stat-item`)
   - **Heading**: Number (class: `stat-number`)
   - **Text Block**: Label (class: `stat-label`)

#### Features Section
1. Add **Section** (class: `features`)
2. Add **Container**
3. Add section header with:
   - **Heading H2** (class: `section-title`)
   - **Paragraph** (class: `section-subtitle`)
4. Add **Div Block** with CSS Grid (class: `features-grid`, 3 columns)
5. For each of the 9 features:
   - Add **Div Block** (class: `feature-card`)
   - Inside: Add icon div, heading, and description

#### Footer CTA
1. Add **Section** (class: `footer-cta`)
2. Add **Container**
3. Add **Heading H2**, **Paragraph**, and **Link Button**

#### Footer
1. Add **Section** (class: `footer`)
2. Add **Container**
3. Add logo and copyright text

### Step 4: Apply Classes
Match the class names from the HTML file to your Webflow elements. The CSS is already in custom code, so classes will automatically apply.

---

## Option 3: HTML Export and Import (Advanced)

### Step 1: Export from Webflow
1. Create a blank Webflow project
2. In **Site Settings** > **General**, enable **Export Code**
3. Export your site's ZIP file

### Step 2: Replace Files
1. Extract the ZIP
2. Replace the `index.html` with the Honua teaser `index.html`
3. Re-zip the files

### Step 3: Re-import
1. Create a new Webflow project
2. Import the modified ZIP file

**Note:** This method is complex and not recommended unless you're very familiar with Webflow.

---

## Setting Up the Email Newsletter

### Option A: Webflow Forms (Easiest)
1. The form will automatically work with Webflow's form handling
2. Go to **Site Settings** > **Forms** to configure:
   - Email notifications
   - Form submission redirect
   - reCAPTCHA
3. View submissions in **Site Settings** > **Forms** > **Form Submissions**

### Option B: Mailchimp Integration
1. Sign up for Mailchimp (free tier available)
2. Create a new audience
3. Go to **Audience** > **Signup forms** > **Embedded forms**
4. Copy the form action URL
5. In Webflow, select your form and set the **Action** field to the Mailchimp URL
6. Set **Method** to POST

### Option C: ConvertKit Integration
1. Sign up for ConvertKit
2. Create a new form
3. Get the form embed code or API endpoint
4. In Webflow, add the form action URL
5. Or use ConvertKit's custom HTML embed

### Option D: Custom Webhook
1. Set up a webhook endpoint (e.g., Zapier, Make.com, or custom API)
2. In Webflow form settings, set the **Action** URL to your webhook
3. Process the email data on your backend

---

## Customization Tips

### Colors
The site uses CSS custom properties (variables). To change colors:
1. In **Custom Code** > **Head Code**, modify these values:
```css
--primary-color: #2563eb;  /* Main brand color */
--primary-dark: #1e40af;   /* Darker shade for hover */
--text-primary: #0f172a;   /* Main text color */
--text-secondary: #475569; /* Secondary text */
```

### Content
- Update the hero headline to match your messaging
- Modify feature cards to highlight different aspects
- Change stats numbers to reflect your data
- Update footer copyright year

### Responsive Design
The site is fully responsive. Test on:
- Desktop (1920px, 1440px, 1280px)
- Tablet (768px)
- Mobile (375px, 414px)

---

## Testing Checklist

Before publishing, test:
- [ ] Form submission works
- [ ] Email notifications are received
- [ ] All links work
- [ ] Mobile responsiveness
- [ ] Page load speed
- [ ] Cross-browser compatibility (Chrome, Firefox, Safari)
- [ ] Social sharing meta tags (add in Webflow SEO settings)

---

## SEO Optimization

### Add in Webflow Page Settings:
1. **Title Tag**: "HonuaIO - Cloud-Native Geospatial Platform | Coming Soon"
2. **Meta Description**: "The modern geospatial platform built for the cloud. Standards-compliant. Plugin-powered. From GeoETL to AI-driven analytics. Join our waitlist."
3. **Open Graph Image**: Create a 1200x630px image with your branding
4. **Twitter Card**: Summary with large image

---

## Performance Tips

1. **Enable Webflow CDN** - Automatic in all plans
2. **Optimize images** - Use WebP format if adding images
3. **Lazy loading** - Enable for images below the fold
4. **Minify code** - Webflow does this automatically
5. **Enable Gzip compression** - Automatic in Webflow hosting

---

## Going Live

1. **Custom Domain**:
   - Go to **Project Settings** > **Hosting**
   - Add your custom domain
   - Update DNS records as instructed

2. **SSL Certificate**:
   - Webflow provides free SSL automatically
   - Ensure HTTPS is enforced

3. **Analytics**:
   - Add Google Analytics in **Project Settings** > **Integrations**
   - Or add custom analytics code in **Custom Code**

---

## Support

For Webflow-specific issues:
- [Webflow University](https://university.webflow.com/)
- [Webflow Forum](https://forum.webflow.com/)
- Webflow Support Chat (in Designer)

For HonuaIO-related questions:
- Stay tuned for updates via the newsletter!

---

## File Structure

```
teaser-site/
├── index.html                    # Complete landing page
├── WEBFLOW-INSTRUCTIONS.md       # This file
└── README.md                     # Additional documentation
```

---

## Need Help?

If you encounter issues:
1. Check that all custom code is in the **Head Code** section
2. Verify class names match exactly (case-sensitive)
3. Test form submissions in preview mode before publishing
4. Clear browser cache if styles don't appear

Good luck with your HonuaIO teaser launch! 🚀
