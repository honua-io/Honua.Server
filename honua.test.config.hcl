honua {
  version     = "1.0"
  environment = "test"
  log_level   = "info"
}

data_source "test_db" {
  provider   = "sqlite"
  connection = "Data Source=/home/mike/projects/Honua.Server/data/ogc-sample.db"
  pool = {
    min_size = 1
    max_size = 5
  }
}

service "wfs" {
  enabled = true
}

service "wms" {
  enabled = true
}

service "wmts" {
  enabled = true
}

service "wcs" {
  enabled = true
}

service "csw" {
  enabled = true
}

service "ogc_api" {
  enabled   = true
  base_path = "/ogc"
}

service "stac" {
  enabled           = true
  provider          = "sqlite"
  connection_string = "Data Source=/home/mike/projects/Honua.Server/data/stac-catalog.db"
}

service "geoservices" {
  enabled = true
}

layer "roads-primary" {
  title       = "Primary Roads"
  description = "Primary road network for STAC catalog testing"
  data_source = data_source.test_db
  table       = "roads_primary"
  id_field    = "road_id"
  introspect_fields = true
  geometry = {
    column = "geom"
    type   = "Point"
    srid   = 4326
  }
  services = ["wfs", "wms", "wmts", "ogc_api", "geoservices", "stac"]
}
