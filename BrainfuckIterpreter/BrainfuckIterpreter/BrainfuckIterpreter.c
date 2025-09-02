#include <stdio.h>
#include <stdlib.h>
#include <conio.h>
#include <string.h>
#include <windows.h>

#define FILENAME "%s\\..\\..\\test_code.bf"
#define MEMORYSIZE 30000

char full_path[MAX_PATH];
char* get_full_path(const char* relative)
{
    char cwd[MAX_PATH];
    getcwd(cwd, sizeof(cwd));
    snprintf(full_path, sizeof(full_path), relative, cwd);
    return full_path;
}

int main() {
    char* path = get_full_path(FILENAME);
    FILE* fp;
    errno_t err = fopen_s(&fp, path, "r");
    int c;

    if (!fp) return 1;

    long len_code = 0;
    while ((c = getc(fp)) != EOF) len_code++;
    fclose(fp);


    char* code = calloc(len_code + 1, sizeof(char));
    if (!code) return 1;

    fopen_s(&fp, path, "r");
    if (!fp) {
        free(code);
        return 1;
    }

    for (long i = 0; i < len_code; i++)
        code[i] = (char)getc(fp);
    code[len_code] = '\0';
    fclose(fp);

    unsigned char memory[MEMORYSIZE] = { 0 };
    unsigned long cursor_position = 0;

    for (long i = 0; i < len_code; i++) {
        switch (code[i]) {
        case '>':
            cursor_position++;
            break;
        case '<':
            cursor_position--;
            break;
        case '+':
            memory[cursor_position]++;
            break;
        case '-':
            memory[cursor_position]--;
            break;
        case '.':
            printf("%c", memory[cursor_position]);
            break;
        case ',':
            memory[cursor_position] = (char)_getch();
            break;
        case '[':
            if (memory[cursor_position] == 0) {
                int depth = 1;
                while (depth > 0 && i < len_code - 1) {
                    i++;
                    if (code[i] == '[') depth++;
                    else if (code[i] == ']') depth--;
                }
            }
            break;
        case ']':
            if (memory[cursor_position] != 0) {
                int depth = 1;
                while (depth > 0 && i > 0) {
                    i--;
                    if (code[i] == ']') depth++;
                    else if (code[i] == '[') depth--;
                }
            }
            break;
        }
    }
    free(code);

    return 0;
}