import assert from 'node:assert/strict'
import { fileURLToPath } from 'node:url'
import { test } from 'node:test'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { MemoryRouter, useLocation } from 'react-router'
import { createServer } from 'vite'

const webRoot = fileURLToPath(new URL('../../../', import.meta.url))

function authSessionValue() {
  return {
    user: null,
    accounts: [],
    status: 'signed-out',
    error: null,
    switchingAccountId: null,
    acceptAuthenticatedUser() {},
    async switchAccount() {},
    async removeAccount() {},
    forgetAccount() {},
    async signOut() {},
    async signOutEverywhere() {},
    async updateUser() { return null },
  }
}

test('WEB-ERROR-001 unknown React paths retain their URL and render recovery in the site shell', async () => {
  const vite = await createServer({
    root: webRoot,
    configFile: `${webRoot}/vite.config.ts`,
    appType: 'custom',
    server: { middlewareMode: true },
  })

  try {
    const [{ default: AppRoutes }, { AuthSessionContext }] = await Promise.all([
      vite.ssrLoadModule('/src/app/routes.tsx'),
      vite.ssrLoadModule('/src/features/auth/authSession.ts'),
    ])

    function RequestedPath() {
      const location = useLocation()
      return createElement('output', { 'data-requested-path': location.pathname }, location.pathname)
    }

    function renderAt(path) {
      return renderToStaticMarkup(
        createElement(MemoryRouter, { initialEntries: [path] },
          createElement(AuthSessionContext.Provider, { value: authSessionValue() },
            createElement('div', null,
              createElement(AppRoutes),
              createElement(RequestedPath),
            ),
          ),
        ),
      )
    }

    const notFoundHtml = renderAt('/signsin')
    const notFoundHeader = notFoundHtml.indexOf('<header')
    const notFoundMain = notFoundHtml.indexOf('<main')
    const notFoundFooter = notFoundHtml.indexOf('<footer')
    assert.ok(notFoundHeader >= 0, 'the shared site header should render')
    assert.ok(notFoundMain > notFoundHeader, '404 content should render after the header in main')
    assert.ok(notFoundFooter > notFoundMain, 'the shared footer should render after main')
    assert.match(notFoundHtml, /This cove isn’t on our chart\./)
    assert.match(notFoundHtml, /The coast is still here, so let’s find another way in\./)
    assert.doesNotMatch(notFoundHtml, /The coast is still here; let’s find another way in\./)
    assert.match(notFoundHtml, /<output data-requested-path="\/signsin">\/signsin<\/output>/)

    const serverErrorHtml = renderAt('/500')
    const serverErrorHeader = serverErrorHtml.indexOf('<header')
    const serverErrorMain = serverErrorHtml.indexOf('<main')
    const serverErrorFooter = serverErrorHtml.indexOf('<footer')
    assert.ok(serverErrorHeader >= 0, 'the shared header should render for server recovery')
    assert.ok(serverErrorMain > serverErrorHeader, '500 content should render after the header in main')
    assert.ok(serverErrorFooter > serverErrorMain, 'the shared footer should render after server recovery')
    assert.match(serverErrorHtml, /A current interrupted the journey\./)
    assert.match(serverErrorHtml, /<output data-requested-path="\/500">\/500<\/output>/)
  } finally {
    await vite.close()
  }
})
