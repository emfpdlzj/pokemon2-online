# LLM Operations

현재 클라이언트는 브라우저에서 직접 모델을 호출하지 않고, 서버의 `/api/llm/reply`, `/api/llm/choices` 엔드포인트만 사용한다.

## 엔드포인트 역할

- `/api/llm/reply`
  - `rival`, `binna` 캐릭터 프롬프트를 서버에서 선택한다.
  - 응답이 비정상이거나 provider 호출이 실패하면 캐릭터별 고정 fallback 문구를 반환한다.
- `/api/llm/choices`
  - 플레이어 대화 선택지 3개를 JSON 형태로 생성한다.
  - provider 응답을 JSON으로 파싱할 수 없으면 서버 fallback 선택지 3개를 반환한다.

## 프롬프트 규칙

- `rival`
  - 밝고 경쟁심 있는 소꿉친구 말투를 유지한다.
  - 첫 마을과 연구소 주변 정보만 사용한다.
  - AI 언급, 미래 스토리 스포일러, 현대 인터넷 용어를 금지한다.
- `binna`
  - 다정하고 응원하는 소꿉친구 말투를 유지한다.
  - 첫 마을, 연구소, 1번 도로, 나팔꽃마을 초반 구간까지만 안다.
  - AI 언급, 미래 스토리 스포일러, 현대 인터넷 용어를 금지한다.
- `choices`
  - 항상 3개 선택지를 JSON `{"choices":[...]}` 로만 반환해야 한다.
  - 플레이어 선택지는 친근함, 침착함, 호기심 축을 유지한다.
  - 체육관, 전설 포켓몬, 미래 지역 같은 범위 밖 정보를 추가하지 않는다.

## 응답 검증 정책

- 공통 입력 검증
  - 빈 메시지는 400으로 거절한다.
- `reply` 검증
  - 60자 이하만 허용한다.
  - 한글, 영문, 숫자, 공백, 기본 문장부호 외 문자가 섞이면 fallback 한다.
  - 문장 종결 부호 기준 3문장을 넘기면 fallback 한다.
- `choices` 검증
  - 코드펜스를 제거한 뒤 JSON으로 파싱한다.
  - `choices` 배열이 없거나 비어 있으면 fallback 한다.
  - 최대 3개만 사용한다.

## 운영 설정

공통 기본값:

- `POKEMON2_LLM_API_KEY`
- `POKEMON2_LLM_API_URL`
- `POKEMON2_LLM_MODEL`
- `POKEMON2_LLM_RATE_LIMIT_PER_MINUTE`

엔드포인트별 override:

- `POKEMON2_LLM_REPLY_MODEL`
- `POKEMON2_LLM_REPLY_MAX_OUTPUT_TOKENS`
- `POKEMON2_LLM_REPLY_PROMPT_COST_PER_1K_USD`
- `POKEMON2_LLM_REPLY_COMPLETION_COST_PER_1K_USD`
- `POKEMON2_LLM_CHOICES_MODEL`
- `POKEMON2_LLM_CHOICES_MAX_OUTPUT_TOKENS`
- `POKEMON2_LLM_CHOICES_PROMPT_COST_PER_1K_USD`
- `POKEMON2_LLM_CHOICES_COMPLETION_COST_PER_1K_USD`

공통 값이 있으면 `reply`, `choices`는 이를 기본값으로 사용하고, 엔드포인트별 값이 있으면 override 한다.

## 호출 제한과 추적

- 서버는 분당 `POKEMON2_LLM_RATE_LIMIT_PER_MINUTE` 기준으로 `reply`, `choices` 요청을 각각 제한한다.
- 제한 키는 가능하면 플레이어 identity, 없으면 `X-Forwarded-For` 또는 remote IP를 사용한다.
- `/api/admin/metrics` 와 Datadog 지표에서 아래 항목을 누적 추적한다.
  - `reply/choices` 요청 수
  - 성공 수
  - fallback 수
  - 실패 사유별 카운트 (`rate_limited`, `provider_error`, `invalid_response`, `not_configured`)
  - prompt/completion/total token
  - 누적 예상 비용 USD

## fallback 정책

- `rival`: `나중에 다시 말 걸어줘!`
- `binna`: `또 얘기하자!`
- `choices`: 입력 맥락에 맞춘 3개 기본 선택지

클라이언트 네트워크 오류 시에도 같은 fallback 문구를 사용해 서버 장애와 provider 장애 모두에서 UX 차이를 줄인다.
