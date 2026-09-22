# Third-party skill notices

The portable skills under `.agents/skills` are selected and adapted for
BLUEVERSE. Their exact upstream repositories and pinned revisions are recorded
in [`skills.json`](skills.json). BLUEVERSE-owned rules and skills remain the
authoritative compatibility layer.

## Vercel React Best Practices

- Source: <https://github.com/vercel-labs/agent-skills/tree/063bee94c3f4df8453406c830b0a7df0f2860278/skills/react-best-practices>
- License: MIT, declared by the upstream skill metadata and README at the
  pinned revision. The upstream repository did not expose a root license file
  at that revision.

## Flutter agent skills

- Source: <https://github.com/flutter/agent-plugins/tree/e89522a8b0c23d282e0acc9538dd0b2cd164358b/skills>
- License: BSD 3-Clause.

```text
Copyright 2026 The Flutter Authors. All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
this list of conditions and the following disclaimer in the documentation
and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its contributors
may be used to endorse or promote products derived from this software without
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## .NET agent skills

- Source: <https://github.com/dotnet/skills/tree/8bbfe7a4d1c5c0cd42cd04e38031779c75f2dda3>
- License: MIT.

```text
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Verified but deferred database candidates

These resources are recorded in [`skills.json`](skills.json) for reproducible
future review but are not vendored into `.agents/skills` and therefore are not
part of the repository's required runtime or agent context:

- [Microsoft PostgreSQL skills](https://github.com/microsoft/postgres-skills/tree/adadc036a652e5d8017db5e659e8b34c42292170/plugin/skills/postgresql-best-practices) — MIT. The upstream plugin also bundles live database tooling and broad operational guidance.
- [Testcontainers for .NET skill](https://github.com/testcontainers/claude-skills/tree/5263fe47160c3ef187b93de4b21d2d7380bce12e/plugins/testcontainers/skills/testcontainers-dotnet) — MIT. The repository will reconsider it when a disposable PostgreSQL fixture is adopted.
- [PostgreSQL migration skill](https://github.com/timescale/pg-aiguide/tree/2bf9f11df6175d555e9a7f63325b226dca71c1b7/skills/postgres-database-migration) — Apache-2.0. The current EF migration surface does not yet require its production-scale, zero-downtime guidance.

No source text from these deferred resources is copied into this repository.
