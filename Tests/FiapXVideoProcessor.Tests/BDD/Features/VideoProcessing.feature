# language: pt
Funcionalidade: Processamento de Vídeo
  Como o serviço de processamento de vídeos
  Eu quero processar vídeos recebidos via fila SQS
  Para extrair frames e disponibilizar como ZIP no S3

  Cenário: Processamento de vídeo com sucesso
    Dado que recebi uma mensagem para processar o vídeo "video-001" do bucket "my-bucket"
    E o vídeo não está sendo processado atualmente
    Quando o processamento é executado
    Então o resultado deve ser sucesso
    E o status enviado ao Video Manager deve ser "Completed"

  Cenário: Mensagem duplicada deve ser ignorada
    Dado que recebi uma mensagem para processar o vídeo "video-dup" do bucket "my-bucket"
    E o vídeo já está sendo processado
    Quando o processamento é executado
    Então o resultado deve ser sucesso
    E o vídeo não deve ser baixado do S3

  Cenário: Falha no download do vídeo
    Dado que recebi uma mensagem para processar o vídeo "video-fail" do bucket "my-bucket"
    E o vídeo não está sendo processado atualmente
    E o download do S3 vai falhar com erro "S3 download failed"
    Quando o processamento é executado
    Então o resultado deve ser falha
    E o status enviado ao Video Manager deve ser "Failed"
    E a chave de cache deve ser removida

  Cenário: Falha na extração de frames
    Dado que recebi uma mensagem para processar o vídeo "video-ffmpeg" do bucket "my-bucket"
    E o vídeo não está sendo processado atualmente
    E a extração de frames vai falhar com erro "FFmpeg falhou"
    Quando o processamento é executado
    Então o resultado deve ser falha
    E o status enviado ao Video Manager deve ser "Failed"

  Cenário: Upload do ZIP para o S3
    Dado que recebi uma mensagem para processar o vídeo "video-zip" do bucket "my-bucket"
    E o vídeo não está sendo processado atualmente
    Quando o processamento é executado
    Então o ZIP deve ser enviado para o caminho "outputs/video-zip/video-zip.zip" no S3

  Cenário: Callback de falha ao Video Manager também falha
    Dado que recebi uma mensagem para processar o vídeo "video-cb" do bucket "my-bucket"
    E o vídeo não está sendo processado atualmente
    E o download do S3 vai falhar com erro "download error"
    E o callback ao Video Manager vai falhar
    Quando o processamento é executado
    Então o resultado deve ser falha