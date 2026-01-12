#include <stdio.h>
#include <stdlib.h>

typedef struct {
	size_t length;
	const char *start;
} string;

void print_string(string str)
{
	fputs(str.start, stdout);
}

void print_i32(__int32 num)
{
	if (num > 9)
    {
		int a = num / 10;
        num -= 10 * a;
        print_i32(a);
    }
    putchar('0'+num);
}

void* allocate(size_t size) { return malloc(size); }
