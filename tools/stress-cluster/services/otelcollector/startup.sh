#!/bin/sh

set -ex

source $ENV_FILE
export APPLICATIONINSIGHTS_CONNECTION_STRING
/otelcol-contrib --config otel-collector-config.yml $@
