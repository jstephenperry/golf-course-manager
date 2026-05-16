set shell := ["bash", "-cu"]

default:
    @just --list

install:
    npm install

dev:
    npm run dev

build:
    npm run build

preview:
    npm run preview

typecheck:
    npm run typecheck

clean:
    rm -rf dist node_modules/.vite

clean-all:
    rm -rf dist node_modules node_modules/.vite

reinstall: clean-all install

ci: install typecheck build
