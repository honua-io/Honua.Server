-- =====================================================
-- SAML Single Sign-On (SSO) Schema
-- Migration: 010_SamlSso.sql
-- Version: 010
-- Dependencies: 001-009
-- =====================================================

-- SAML Identity Provider configurations (per customer/tenant)
CREATE TABLE IF NOT EXISTS saml_identity_providers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id VARCHAR(100) NOT NULL,  -- Changed from tenant_id UUID to match customers table
    name VARCHAR(255) NOT NULL,
    entity_id TEXT NOT NULL,
    single_sign_on_service_url TEXT NOT NULL,
    single_logout_service_url TEXT,
    signing_certificate TEXT NOT NULL,
    sign_authentication_requests BOOLEAN NOT NULL DEFAULT true,
    want_assertions_signed BOOLEAN NOT NULL DEFAULT true,
    binding_type VARCHAR(50) NOT NULL DEFAULT 'HttpPost',
    attribute_mappings JSONB NOT NULL DEFAULT '{}',
    enable_jit_provisioning BOOLEAN NOT NULL DEFAULT true,
    default_role VARCHAR(100) NOT NULL DEFAULT 'User',
    enabled BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    metadata_xml TEXT,
    allow_unsolicited_authn_response BOOLEAN NOT NULL DEFAULT false,
    name_id_format VARCHAR(255) NOT NULL DEFAULT 'urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress',

    CONSTRAINT fk_saml_idp_customer FOREIGN KEY (customer_id) REFERENCES customers(customer_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_saml_idp_customer ON saml_identity_providers(customer_id);
CREATE INDEX IF NOT EXISTS idx_saml_idp_enabled ON saml_identity_providers(customer_id, enabled) WHERE enabled = true;
CREATE INDEX IF NOT EXISTS idx_saml_idp_customer_fk ON saml_identity_providers(customer_id);

-- SAML authentication sessions (temporary storage for SAML flow)
CREATE TABLE IF NOT EXISTS saml_sessions (
    id VARCHAR(50) PRIMARY KEY,
    customer_id VARCHAR(100) NOT NULL,  -- Changed from tenant_id UUID
    idp_configuration_id UUID NOT NULL,
    request_id VARCHAR(100) NOT NULL UNIQUE,
    relay_state TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL,
    consumed BOOLEAN NOT NULL DEFAULT false,

    CONSTRAINT fk_saml_session_customer FOREIGN KEY (customer_id) REFERENCES customers(customer_id) ON DELETE CASCADE,
    CONSTRAINT fk_saml_session_idp FOREIGN KEY (idp_configuration_id) REFERENCES saml_identity_providers(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_saml_sessions_customer ON saml_sessions(customer_id);
CREATE INDEX IF NOT EXISTS idx_saml_sessions_idp ON saml_sessions(idp_configuration_id);
CREATE INDEX IF NOT EXISTS idx_saml_sessions_request_id ON saml_sessions(request_id) WHERE NOT consumed;
CREATE INDEX IF NOT EXISTS idx_saml_sessions_expires ON saml_sessions(expires_at) WHERE NOT consumed;

-- SAML user mappings (track users created via JIT provisioning)
CREATE TABLE IF NOT EXISTS saml_user_mappings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id VARCHAR(100) NOT NULL,  -- Changed from tenant_id UUID
    user_id UUID NOT NULL,
    idp_configuration_id UUID NOT NULL,
    name_id VARCHAR(500) NOT NULL,
    session_index VARCHAR(500),
    last_login_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_saml_user_customer FOREIGN KEY (customer_id) REFERENCES customers(customer_id) ON DELETE CASCADE,
    CONSTRAINT fk_saml_user_idp FOREIGN KEY (idp_configuration_id) REFERENCES saml_identity_providers(id) ON DELETE CASCADE,
    CONSTRAINT uq_saml_user_mapping UNIQUE (customer_id, idp_configuration_id, name_id)
);

CREATE INDEX IF NOT EXISTS idx_saml_user_mappings_user ON saml_user_mappings(user_id);
CREATE INDEX IF NOT EXISTS idx_saml_user_mappings_customer ON saml_user_mappings(customer_id);
CREATE INDEX IF NOT EXISTS idx_saml_user_mappings_idp ON saml_user_mappings(idp_configuration_id);
CREATE INDEX IF NOT EXISTS idx_saml_user_mappings_customer_idp ON saml_user_mappings(customer_id, idp_configuration_id);

-- Cleanup function for expired SAML sessions
CREATE OR REPLACE FUNCTION cleanup_expired_saml_sessions()
RETURNS INTEGER
LANGUAGE plpgsql
AS $$
DECLARE
    deleted_count INTEGER;
BEGIN
    DELETE FROM saml_sessions
    WHERE expires_at < NOW();

    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RETURN deleted_count;
END;
$$;

-- Comments for documentation
COMMENT ON TABLE saml_identity_providers IS 'SAML 2.0 Identity Provider configurations per tenant for Enterprise SSO';
COMMENT ON TABLE saml_sessions IS 'Temporary SAML authentication sessions to prevent replay attacks';
COMMENT ON TABLE saml_user_mappings IS 'Mapping between SAML NameIDs and internal user accounts for JIT provisioning';

COMMENT ON COLUMN saml_identity_providers.entity_id IS 'IdP EntityID (issuer identifier)';
COMMENT ON COLUMN saml_identity_providers.single_sign_on_service_url IS 'IdP SSO endpoint URL';
COMMENT ON COLUMN saml_identity_providers.signing_certificate IS 'X.509 certificate for validating SAML assertions (PEM format)';
COMMENT ON COLUMN saml_identity_providers.binding_type IS 'SAML binding: HttpPost or HttpRedirect';
COMMENT ON COLUMN saml_identity_providers.attribute_mappings IS 'JSON mapping of SAML attributes to user claims';
COMMENT ON COLUMN saml_identity_providers.enable_jit_provisioning IS 'Enable Just-in-Time user provisioning on first login';
COMMENT ON COLUMN saml_identity_providers.metadata_xml IS 'Full IdP metadata XML document';

COMMENT ON COLUMN saml_sessions.request_id IS 'SAML AuthnRequest ID to correlate request/response';
COMMENT ON COLUMN saml_sessions.relay_state IS 'Relay state (return URL after authentication)';
COMMENT ON COLUMN saml_sessions.consumed IS 'Whether this session has been consumed (prevents replay)';

COMMENT ON COLUMN saml_user_mappings.name_id IS 'SAML NameID from IdP assertion';
COMMENT ON COLUMN saml_user_mappings.session_index IS 'SAML SessionIndex for single logout';
