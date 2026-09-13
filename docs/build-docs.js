const fs = require('fs');
const path = require('path');

function parseTxtFile(filePath) {
    if (!fs.existsSync(filePath)) return [];
    const content = fs.readFileSync(filePath, 'utf-8');
    const blocks = content.split('---------');
    const endpoints = [];

    for (const block of blocks) {
        const lines = block.trim().split('\n').map(l => l.trim()).filter(l => l.length > 0);
        if (lines.length === 0) continue;

        let moduleName = "General";
        let method = "GET";
        let apiPath = "/";
        let purpose = "";
        let inputInfo = "";
        let outputInfo = "";

        for (const line of lines) {
            if (line.startsWith("Module phụ trách:")) {
                moduleName = line.replace("Module phụ trách:", "").trim();
            } else if (line.startsWith("SignalR")) {
                const parts = line.split(/\s+/);
                method = "SIGNALR";
                apiPath = parts[1] || line;
            } else if (["GET", "POST", "PUT", "PATCH", "DELETE"].some(m => line.startsWith(m))) {
                const parts = line.split(/\s+/);
                method = parts[0];
                apiPath = parts[1] || "/";
            } else if (line.startsWith("Mục đích:")) {
                purpose = line.replace("Mục đích:", "").trim();
            } else if (line.startsWith("Input chính:")) {
                inputInfo = line.replace("Input chính:", "").trim();
            } else if (line.startsWith("Output trả về:")) {
                outputInfo = line.replace("Output trả về:", "").trim();
            }
        }

        if (apiPath !== "/") {
            endpoints.push({ module: moduleName, method, path: apiPath, purpose, inputInfo, outputInfo });
        }
    }
    return endpoints;
}

const projectRoot = path.resolve(__dirname, '..');
const file1 = path.join(projectRoot, 'Module phụ trách - Commission.txt');
const file2 = path.join(projectRoot, 'Module phụ trách.txt');

const ep1 = parseTxtFile(file1);
const ep2 = parseTxtFile(file2);
const allEndpoints = [...ep1, ...ep2];

const openapiSpec = {
    openapi: "3.0.3",
    info: {
        title: "ArtCommission (Dillustration) - Complete API Specification",
        description: "Tài liệu API đầy đủ cho toàn bộ các Phân hệ. Đọc trực tiếp trên trình duyệt mà không cần khởi chạy backend server.",
        version: "1.0.0"
    },
    servers: [
        { url: "https://api.dillustration.com/api/v1", description: "Production Server" },
        { url: "https://localhost:7001/api/v1", description: "Local Development Server" }
    ],
    paths: {},
    components: {
        securitySchemes: {
            BearerAuth: {
                type: "http",
                scheme: "bearer",
                bearerFormat: "JWT",
                description: "Nhập JWT Bearer Token để chạy thử nghiệm API"
            }
        }
    },
    security: [{ BearerAuth: [] }]
};

const tagsSet = new Set();

for (const ep of allEndpoints) {
    tagsSet.add(ep.module);
    let cleanPath = ep.path.replace("/api/v1", "");
    if (!cleanPath.startsWith("/")) cleanPath = "/" + cleanPath;

    if (!openapiSpec.paths[cleanPath]) {
        openapiSpec.paths[cleanPath] = {};
    }

    let method = ep.method.toLowerCase();
    const parameters = [];

    const pathParamsMatches = cleanPath.match(/\{([a-zA-Z0-9_]+)\}/g);
    if (pathParamsMatches) {
        for (const m of pathParamsMatches) {
            const paramName = m.replace(/[\{\}]/g, "");
            parameters.push({
                name: paramName,
                in: "path",
                required: true,
                schema: { type: "string" },
                description: `ID tham chiếu ${paramName}`
            });
        }
    }

    const operation = {
        tags: [ep.module],
        summary: ep.purpose,
        description: `**Mục đích**: ${ep.purpose}\n\n**Input chính**: ${ep.inputInfo}\n\n**Output trả về**: \`${ep.outputInfo}\``,
        responses: {
            "200": {
                description: "Thực thi thành công",
                content: {
                    "application/json": {
                        example: {
                            data: ep.outputInfo,
                            meta: null,
                            error: null
                        }
                    }
                }
            },
            "400": { description: "Lỗi dữ liệu đầu vào (ProblemDetails RFC7807)" },
            "401": { description: "Chưa đăng nhập (Unauthorized)" },
            "403": { description: "Không có quyền thực thi (Forbidden)" }
        }
    };

    if (parameters.length > 0) {
        operation.parameters = parameters;
    }

    if (["post", "put", "patch"].includes(method) && ep.inputInfo) {
        operation.requestBody = {
            required: true,
            content: {
                "application/json": {
                    example: { details: ep.inputInfo }
                }
            }
        };
    }

    if (method === "signalr") {
        operation.summary = `[SignalR Hub] ${ep.purpose}`;
        method = "get";
    }

    openapiSpec.paths[cleanPath][method] = operation;
}

openapiSpec.tags = Array.from(tagsSet).sort().map(t => ({ name: t }));

const jsonSpecStr = JSON.stringify(openapiSpec, null, 2);

const htmlContent = `<!DOCTYPE html>
<html lang="vi">
  <head>
    <title>ArtCommission (Dillustration) - Complete API Reference</title>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <link rel="icon" type="image/svg+xml" href="https://scalar.com/favicon.svg" />
    <style>
      body {
        margin: 0;
        padding: 0;
      }
    </style>
  </head>
  <body>
    <script id="api-reference" type="application/json">
${jsonSpecStr}
    </script>
    <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
  </body>
</html>
`;

const outputPath = path.join(__dirname, 'api-docs.html');
fs.writeFileSync(outputPath, htmlContent, 'utf-8');
console.log(`SUCCESS: Updated static Scalar API docs HTML at: ${outputPath}`);
