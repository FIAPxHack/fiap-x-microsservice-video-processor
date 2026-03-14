# Video Processor - Microsserviço de Processamento de Vídeo

## Descrição

Microsserviço responsável por consumir mensagens de uma fila SQS, processar vídeos utilizando FFmpeg (extração de frames) e gerar um arquivo ZIP com os resultados. Ao final do processamento, realiza um callback HTTP para o Video Manager atualizando o status do vídeo.

## Arquitetura

```
S3 (novo arquivo) → SQS (mensagem) → Video Processor (consumer) 
→ Download do vídeo (S3)
→ Extração de frames (FFmpeg)
→ Criação do ZIP
→ Upload do ZIP (S3)
→ Callback HTTP → Video Manager (atualiza status)
```

### Diagrama de Contexto 

O Video Processor faz parte de uma arquitetura de microsserviços que inclui:

- **API Gateway** — Ponto de entrada das requisições
- **Video Manager** — Gerencia uploads, metadados e status dos vídeos (PostgreSQL)
- **Video Processor** — Processa vídeos de forma assíncrona (este serviço)
- **Notificação** — Notifica o usuário sobre o resultado do processamento
- **Monitoramento** — Observabilidade dos serviços

## Estrutura de Pastas

```
video-processor/
├── Core/
│ ├── VideoProcessor.Application/ # Camada de aplicação (casos de uso, interfaces, DTOs)
│ │ ├── Configurations/
│ │ ├── Dtos/
│ │ ├── Enums/
│ │ ├── Interfaces/
│ │ └── UseCases/
│ │ └── ProcessVideo/
│ └── VideoProcessor.Domain/ # Camada de domínio (entidades)
│ └── Entities/
├── Infrastructure/
│ └── VideoProcessor.Infrastructure/ # Infraestrutura (SQS, S3, FFmpeg, HTTP Client)
│ ├── Configurations/
│ ├── HttpClients/
│ ├── Messaging/
│ ├── Processing/
│ └── Storage/
├── Worker/
│ └── VideoProcessor.Worker/ # Host da aplicação (BackgroundService)
│ ├── Workers/
│ ├── Program.cs
│ ├── appsettings.json
│ └── appsettings.Development.json
├── Tests/
│ └── VideoProcessor.Tests/
├── Dockerfile
├── README.md
└── VideoProcessor.sln
```

## Descrição dos Principais Diretórios

- **Core/VideoProcessor.Application/**: Casos de uso do processamento de vídeo, interfaces (abstrações de SQS, S3, FFmpeg, HTTP Client) e DTOs.
- **Core/VideoProcessor.Domain/**: Entidades de domínio e enums (ex: `VideoProcessingStatus`).
- **Infrastructure/VideoProcessor.Infrastructure/**: Implementações concretas — consumer SQS, client S3, wrapper do FFmpeg e HTTP client para callback ao Video Manager.
- **Worker/VideoProcessor.Worker/**: Ponto de entrada da aplicação. Utiliza `BackgroundService` para polling contínuo na fila SQS. Expõe endpoint `/healthz` para health check do Kubernetes.

## Tecnologias

- **.NET 10** — Runtime e SDK
- **FFmpeg** — Extração de frames dos vídeos
- **Amazon SQS** — Fila de mensagens (consumer)
- **Amazon S3** — Armazenamento de vídeos e ZIPs
- **MediatR** — Mediador para casos de uso (CQRS)
- **Polly** — Resiliência em chamadas HTTP (retry com backoff exponencial)
- **Redis** — Cache para idempotência (evitar reprocessamento)

## Fluxo de Processamento

1. Um novo vídeo é enviado ao bucket S3 pelo Video Manager
2. O S3 dispara uma notificação que gera uma mensagem na fila SQS
3. O Video Processor consome a mensagem da fila
4. O vídeo é baixado do S3 para processamento local
5. O FFmpeg extrai os frames do vídeo
6. Os frames são compactados em um arquivo ZIP
7. O ZIP é enviado de volta ao S3
8. O Video Processor faz um callback HTTP ao Video Manager com o status final (`completed` ou `failed`)
9. A mensagem é removida da fila SQS

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Docker](https://www.docker.com/get-started)
- [FFmpeg](https://ffmpeg.org/download.html) (para execução local)
- Conta AWS com acesso a S3 e SQS (ou LocalStack para desenvolvimento local)

## Como Executar

### Localmente

```sh
# Instalar FFmpeg (Ubuntu/Debian)
sudo apt-get update && sudo apt-get install -y ffmpeg

# Executar a aplicação
dotnet run --project Worker/VideoProcessor.Worker/VideoProcessor.Worker.csproj
```

### Com Docker

```sh
# Build da imagem
docker build -t video-processor .

# Executar o container
docker run -d \
  -e AWS_REGION=us-east-1 \
  -e SQS_QUEUE_URL=<url-da-fila> \
  -e S3_BUCKET_NAME=<nome-do-bucket> \
  -e VIDEO_MANAGER_BASE_URL=<url-do-video-manager> \
  --name video-processor \
  video-processor
```

### Variáveis de Ambiente

|Variável|Descrição|Exemplo|
|---|---|---|
|AWS_REGION|Região AWS|	us-east-1|
|SQS_QUEUE_URL|	URL da fila SQS|https://sqs.us-east-1.amazonaws.com/123/video-queue|
|S3_BUCKET_NAME|Nome do bucket S3|video-storage-bucket|
|VIDEO_MANAGER_BASE_URL|URL base do Video Manager|http://video-manager:8080|
|REDIS_CONNECTION_STRING|Connection string do Redis|localhost:6379|

## Health Check

```
GET /healthz
```

Retorna 200 OK com { "status": "ok" } quando o serviço está saudável.

## Testes

```sh
dotnet test
```

## Resiliência

- SQS Visibility Timeout: Configurado para suportar o tempo de processamento dos vídeos (recomendado: 10-15 min)
- Dead Letter Queue (DLQ): Mensagens que falharem após N tentativas são movidas para a DLQ
- Retry com Polly: Callback HTTP ao Video Manager utiliza retry com backoff exponencial
- Idempotência: Redis é utilizado para evitar reprocessamento de vídeos duplicados

## Infraestrutura (Terraform)

O deploy na AWS é gerenciado via Terraform. Os recursos provisionados incluem:

### Recursos AWS

| Recurso | Descrição |
|---|---|
| **Amazon SQS** | Fila de mensagens para receber eventos de novos vídeos no S3 |
| **Amazon SQS (DLQ)** | Dead Letter Queue para mensagens que falharem após N tentativas |
| **Amazon S3** | Bucket para armazenamento de vídeos e ZIPs processados |
| **Amazon S3 Event Notification** | Trigger que envia mensagem ao SQS quando um novo arquivo é criado |
| **Amazon ECS / EKS** | Orquestração do container do Video Processor |
| **Amazon ECR** | Registry para a imagem Docker |
| **Amazon ElastiCache (Redis)** | Cache para controle de idempotência |
| **IAM Roles & Policies** | Permissões de acesso ao SQS, S3 e Redis |
| **CloudWatch** | Logs e métricas do serviço |

### Estrutura do Terraform

```
terraform/
├── main.tf # Recursos principais
├── variables.tf # Variáveis de entrada
├── outputs.tf # Outputs (URLs, ARNs)
├── provider.tf # Configuração do provider AWS
├── sqs.tf # Fila SQS + DLQ
├── s3.tf # Bucket S3 + Event Notification
├── ecs.tf # Task Definition + Service (ou eks.tf)
├── ecr.tf # Repositório de imagens
├── elasticache.tf # Redis
├── iam.tf # Roles e policies
└── terraform.tfvars # Valores das variáveis (não commitado)
```


### Como aplicar

```sh
cd terraform

# Inicializar
terraform init

# Visualizar o plano
terraform plan

# Aplicar
terraform apply
```

## CI/CD

O pipeline de CI/CD realiza:

1. Build & Test — Compila o projeto e executa os testes
2. nálise estática — SonarQube/SonarCloud
3. Build da imagem Docker — Push para o Amazon ECR
4. Deploy — terraform apply para atualizar a infraestrutura na AWS