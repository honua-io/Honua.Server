# Cloud Marketplace Publishing Guide

This guide walks through the process of publishing Honua IO Server to AWS, Azure, and Google Cloud marketplaces.

## Prerequisites

### All Marketplaces

- [ ] Legal entity registered
- [ ] Tax forms completed (W-8/W-9 for US)
- [ ] Bank account for payments
- [ ] Company information and branding assets
- [ ] Product documentation ready
- [ ] Support infrastructure in place

### Technical Requirements

- [ ] Container images published to public registries
- [ ] Infrastructure templates tested
- [ ] Metering integration implemented and tested
- [ ] Security scanning completed
- [ ] Load testing performed
- [ ] Backup and disaster recovery tested

## AWS Marketplace Publishing

### Step 1: Seller Registration

1. Navigate to [AWS Marketplace Management Portal](https://aws.amazon.com/marketplace/management/)
2. Click "Register as a Seller"
3. Complete seller profile:
   - Company information
   - Tax information (W-8/W-9)
   - Banking information
4. Wait for approval (typically 1-2 business days)

### Step 2: Create Product Listing

1. Go to "Assets" > "Container"
2. Click "Create container product"
3. Fill in product information:
   - **Product Title**: Honua IO Server
   - **SKU**: HONUA-SERVER-001
   - **Short Description**: Copy from LISTING_CONTENT.md
   - **Long Description**: Copy from LISTING_CONTENT.md
   - **Product Logo**: Upload 110x110 PNG
   - **Highlights**: Key features (max 3)
   - **Categories**: Developer Tools, Databases & Analytics
   - **Keywords**: Copy from LISTING_CONTENT.md

### Step 3: Configure Pricing

1. Select pricing model: "Usage-based"
2. Define dimensions:
   ```
   Dimension 1: API Requests (per 1,000)
   Dimension 2: Storage (GB-month)
   Dimension 3: Data Processing (GB)
   Dimension 4: Active Users
   ```
3. Set pricing for each dimension
4. Configure free trial (14 days recommended)

### Step 4: Upload Container Images

1. Push images to ECR Public:
   ```bash
   aws ecr-public get-login-password --region us-east-1 | \
     docker login --username AWS --password-stdin public.ecr.aws

   docker tag ghcr.io/honua-io/honua-server:latest \
     public.ecr.aws/honua/honua-server:latest

   docker push public.ecr.aws/honua/honua-server:latest
   ```

2. In marketplace console:
   - Container images > Add images
   - Enter ECR Public URI
   - Specify supported architectures (amd64, arm64)
   - Add image scanning results

### Step 5: Upload Deployment Templates

1. Go to "Delivery methods" section
2. Upload CloudFormation template:
   - Upload `aws/templates/eks-deployment.yaml`
   - Add deployment instructions
   - Configure launch regions

### Step 6: Configure Usage Instructions

1. Upload deployment guide (aws/docs/README.md)
2. Add "Getting Started" instructions
3. Include architecture diagrams
4. Add FAQ section

### Step 7: Configure Support

1. Support contacts:
   - Email: support@honua.io
   - Support URL: https://support.honua.io
2. Support SLA:
   - Professional: 24-hour response
   - Enterprise: 4-hour response, 24/7

### Step 8: Legal Documents

1. Upload EULA (End User License Agreement)
2. Terms and conditions
3. Privacy policy
4. Refund policy

### Step 9: Submit for Review

1. Review all sections for completeness
2. Click "Submit for review"
3. AWS will review (typically 3-7 business days)
4. Address any feedback
5. Once approved, publish to marketplace

### Step 10: Test Customer Experience

1. Subscribe to your own product (separate AWS account)
2. Deploy using CloudFormation template
3. Verify metering is working
4. Test all documented features
5. Verify billing appears correctly

---

## Azure Marketplace Publishing

### Step 1: Partner Center Registration

1. Navigate to [Microsoft Partner Center](https://partner.microsoft.com/)
2. Enroll in the Commercial Marketplace program
3. Complete partner profile:
   - Company verification
   - Tax profile (W-8/W-9)
   - Payout profile (bank account)
4. Accept marketplace publisher agreement

### Step 2: Create Offer

1. Go to "Commercial Marketplace" > "Overview"
2. Click "New offer" > "Azure Container"
3. Enter offer details:
   - **Offer ID**: honua-io-server
   - **Offer alias**: Honua IO Server
4. Click "Create"

### Step 3: Offer Setup

1. **Offer setup page**:
   - Test drive: Optional (configure preview environment)
   - Customer leads: Configure CRM integration
   - Properties:
     - Categories: Developer Tools, Databases & Analytics
     - Legal: Upload EULA, terms, privacy policy

2. **Offer listing page**:
   - Name: Honua IO Server
   - Search results summary: Copy from LISTING_CONTENT.md
   - Description: Copy long description
   - Getting started instructions: Link to docs
   - Search keywords: Copy keywords
   - Privacy policy URL
   - Support URLs
   - Media:
     - Logos (216x216, 48x48)
     - Screenshots (1280x720)
     - Videos (YouTube/Vimeo links)

### Step 4: Preview Audience

1. Add Azure subscription IDs for preview
2. These subscriptions can access the offer before public release
3. Add at least 2-3 test subscriptions

### Step 5: Technical Configuration

1. **Container repository**:
   - Registry: Azure Container Registry
   - Repository: honua-io/honua-server
   - Image tags: latest, v1.0.0

2. **Push images to ACR**:
   ```bash
   az acr login --name honuaacr
   docker tag ghcr.io/honua-io/honua-server:latest \
     honuaacr.azurecr.io/honua-server:latest
   docker push honuaacr.azurecr.io/honua-server:latest
   ```

3. **ARM template**:
   - Upload `azure/templates/aks-deployment.json`
   - Upload parameters file
   - Configure deployment regions

### Step 6: Plans and Pricing

1. Create plan: "Professional"
   - Plan ID: professional
   - Plan name: Professional
   - Description: For small to medium deployments

2. Create plan: "Enterprise"
   - Plan ID: enterprise
   - Plan name: Enterprise
   - Description: For large-scale deployments

3. Configure metering dimensions:
   ```
   Dimension 1: api-requests (per 1,000)
   Dimension 2: storage-gb (GB-month)
   Dimension 3: data-processing-gb (GB)
   Dimension 4: users (active users)
   ```

4. Set pricing for each dimension

### Step 7: Review and Publish

1. Review all sections
2. Click "Review and publish"
3. Submit for certification
4. Microsoft will review (typically 5-10 business days)
5. Address certification feedback
6. Once certified, go live

### Step 8: Post-Publication

1. Monitor customer acquisition dashboard
2. Respond to customer reviews
3. Update product regularly
4. Monitor metering data
5. Analyze usage patterns

---

## Google Cloud Marketplace Publishing

### Step 9: Producer Portal Registration

1. Navigate to [GCP Producer Portal](https://console.cloud.google.com/producer-portal)
2. Create or select a Google Cloud project
3. Enable required APIs:
   - Cloud Commerce Producer API
   - Service Control API
   - Service Management API

### Step 10: Create Product

1. Go to "Solutions" > "Create"
2. Select "Kubernetes App"
3. Fill in product information:
   - **Product name**: Honua IO Server
   - **Description**: Copy from LISTING_CONTENT.md
   - **Deployment package**: Select project
   - **Display name**: Honua IO Server

### Step 11: Configure Application

1. **Container images**:
   - Push to GCR:
     ```bash
     docker tag ghcr.io/honua-io/honua-server:latest \
       gcr.io/honua-io/honua-server:latest
     gcloud docker -- push gcr.io/honua-io/honua-server:latest
     ```

2. **Deployment configuration**:
   - Upload `gcp/templates/gke-deployment.yaml`
   - Configure schema for user inputs
   - Add application resource definitions

### Step 12: Configure Billing

1. Select billing model: "Usage-based"
2. Define metrics:
   ```
   Metric 1: api_requests (per 1,000 requests)
   Metric 2: storage_gb (GB per month)
   Metric 3: data_processing_gb (GB)
   ```

3. Set pricing for each metric
4. Configure reporting frequency

### Step 13: Product Listing

1. **Marketing**:
   - Tagline
   - Description
   - Features list
   - Screenshots (1920x1080)
   - Demo video
   - Architecture diagram

2. **Documentation**:
   - Quick start guide
   - Administrator guide
   - User guide
   - API documentation
   - Troubleshooting guide

3. **Support**:
   - Support email
   - Support URL
   - Community forum
   - SLA information

### Step 14: Submit for Review

1. Complete all required sections
2. Run validation checks
3. Submit for Google review
4. Review typically takes 2-4 weeks
5. Address feedback
6. Publish when approved

---

## Pre-Launch Checklist

### AWS Marketplace

- [ ] CloudFormation template tested in all supported regions
- [ ] Metering integration verified
- [ ] Container images scanned for vulnerabilities
- [ ] Documentation complete and accurate
- [ ] Support processes in place
- [ ] EULA and legal documents ready
- [ ] Pricing configured correctly
- [ ] Test deployment successful

### Azure Marketplace

- [ ] ARM template tested in all supported regions
- [ ] Metering API integration tested
- [ ] Container images in ACR
- [ ] All legal documents uploaded
- [ ] Support contacts configured
- [ ] Preview deployment successful
- [ ] Pricing and plans configured
- [ ] Lead management configured

### Google Cloud Marketplace

- [ ] Deployment Manager template tested
- [ ] Usage reporting integration tested
- [ ] Container images in GCR
- [ ] Application schema validated
- [ ] Documentation complete
- [ ] Support infrastructure ready
- [ ] Billing integration tested
- [ ] Test installation successful

## Post-Launch Activities

### Week 1

- [ ] Monitor for first customer deployment
- [ ] Check metering data flow
- [ ] Review customer feedback
- [ ] Address any deployment issues
- [ ] Monitor support tickets

### Week 2-4

- [ ] Analyze usage patterns
- [ ] Gather customer feedback
- [ ] Address feature requests
- [ ] Optimize deployment process
- [ ] Update documentation based on questions

### Ongoing

- [ ] Monthly security updates
- [ ] Quarterly feature releases
- [ ] Regular documentation updates
- [ ] Customer success check-ins
- [ ] Pricing optimization
- [ ] Performance monitoring
- [ ] Competitive analysis

## Marketing & Promotion

### Launch Activities

1. **Press Release**:
   - Coordinate with cloud provider PR teams
   - Announce on company blog
   - Share on social media

2. **Content Marketing**:
   - Write blog posts about use cases
   - Create video tutorials
   - Host webinars
   - Publish case studies

3. **Community Engagement**:
   - Engage in relevant forums
   - Answer questions on Stack Overflow
   - Participate in cloud provider communities

4. **Paid Advertising**:
   - Consider marketplace ads
   - Google Ads for relevant keywords
   - LinkedIn sponsored content

### Success Metrics

Track these KPIs:
- Marketplace views
- Trial starts
- Conversion rate (trial to paid)
- Customer acquisition cost (CAC)
- Average contract value (ACV)
- Churn rate
- Net promoter score (NPS)
- Support ticket volume
- Time to first value
- Monthly recurring revenue (MRR)

## Common Issues & Solutions

### Certification Delays

**Issue**: Marketplace review taking longer than expected
**Solution**:
- Ensure all documentation is complete
- Respond promptly to feedback
- Have support contacts readily available
- Provide test accounts if requested

### Metering Integration Failures

**Issue**: Usage not being reported correctly
**Solution**:
- Verify API credentials and permissions
- Check network connectivity to metering APIs
- Implement retry logic
- Add comprehensive logging
- Test with small quantities first

### Customer Onboarding Issues

**Issue**: Customers struggling with deployment
**Solution**:
- Simplify deployment templates
- Add more detailed documentation
- Create video walkthroughs
- Offer onboarding assistance
- Improve error messages

### Pricing Concerns

**Issue**: Customers find pricing unclear or too high
**Solution**:
- Provide pricing calculator
- Offer transparent cost breakdown
- Create comparison with alternatives
- Consider tiered pricing
- Offer volume discounts

## Support Resources

- AWS Marketplace Seller Guide: https://docs.aws.amazon.com/marketplace/latest/userguide/
- Azure Marketplace Documentation: https://docs.microsoft.com/azure/marketplace/
- GCP Marketplace Documentation: https://cloud.google.com/marketplace/docs/partners
- Cloud Marketplace Slack Community: [Join here]
- Monthly seller webinars and office hours

## Conclusion

Publishing to cloud marketplaces is a significant undertaking but provides access to a large customer base. Follow this guide carefully, test thoroughly, and maintain high-quality support to ensure success.

For questions or assistance, contact the Honua IO marketplace team at marketplace@honua.io
