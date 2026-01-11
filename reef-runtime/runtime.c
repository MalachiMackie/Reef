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

void* allocate(size_t size) { return malloc(size); }
