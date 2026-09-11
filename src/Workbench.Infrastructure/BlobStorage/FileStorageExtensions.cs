using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Workbench.Application.Interfaces;

namespace Workbench.Infrastructure.BlobStorage;

public static class FileStorageExtensions
{
    public static IServiceCollection AddFileStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var section = configuration.GetSection(FileStorageOptions.SectionName);
        services.Configure<FileStorageOptions>(section);

        var options = section.Get<FileStorageOptions>() ?? new FileStorageOptions();

        if (options.Provider == FileStorageProvider.Local)
        {
            // 로컬 디스크는 재배포·스케일아웃에서 첨부가 사라진다. 그렇게 하겠다면 명시적으로
            // 켜야 하고, 실수로 그 상태가 되는 것은 막는다.
            if (!isDevelopment && !options.AllowLocalOutsideDevelopment)
            {
                throw new InvalidOperationException(
                    $"{FileStorageOptions.SectionName}:Provider 가 {nameof(FileStorageProvider.Local)} 인데 "
                    + "호스트 환경이 Development 가 아닙니다. 로컬 디스크에 저장하면 재배포·스케일아웃에서 "
                    + $"첨부 파일이 사라집니다. {nameof(FileStorageProvider.AzureBlob)} 을 쓰거나, 단일 서버에 "
                    + $"보관할 의도라면 {FileStorageOptions.SectionName}:"
                    + $"{nameof(FileStorageOptions.AllowLocalOutsideDevelopment)} 를 true 로 설정하세요.");
            }

            services.AddSingleton<IFileStorage, LocalFileStorage>();

            return services;
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                $"{FileStorageOptions.SectionName}:{nameof(FileStorageOptions.ConnectionString)} 이(가) "
                + "없습니다. User Secrets 또는 환경변수에 설정하세요.");
        }

        services.AddSingleton<IFileStorage, AzureBlobFileStorage>();

        return services;
    }
}
